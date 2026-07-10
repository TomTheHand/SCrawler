' Copyright (C) 2023  Andy https://github.com/AAndyProgram
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY
Imports System.Net
Imports System.Threading
Namespace DownloadObjects
    ''' <summary>
    ''' Global DNS-failure circuit breaker.
    '''
    ''' When the user's connection is saturated or temporarily down, every API call fails
    ''' simultaneously with "The remote name could not be resolved". Without intervention
    ''' SCrawler fires off all pending users into guaranteed-failure batches, flooding the
    ''' log with hundreds of identical DNS errors and downloading nothing.
    '''
    ''' This module tracks DNS failures across all concurrent download tasks. Once
    ''' <see cref="FailThreshold"/> failures accumulate within <see cref="FailWindowSecs"/>
    ''' seconds the breaker trips:
    '''   - <see cref="TDownloader.Suspended"/> is set so no new Job threads start.
    '''   - <see cref="WaitForConnectivity"/> blocks the calling Job thread, probing DNS
    '''     every <see cref="ProbeIntervalSec"/> seconds.
    '''   - Once connectivity returns the breaker resets and the Job continues with
    '''     whatever users remain in its queue.
    '''   - If connectivity does not return within <see cref="MaxWaitMinutes"/> minutes
    '''     <see cref="WaitForConnectivity"/> returns False and the caller cancels the run.
    '''
    ''' Note: users that were already removed from the queue during the failing batches
    ''' (before the breaker tripped) are gone for this run but remain in SCrawler's user
    ''' list for the next run. With a threshold of 5 and typical batch sizes of 2-3 that
    ''' is at most ~6-10 users per connectivity outage.
    '''
    ''' Thread safety: _failCount/_windowStart are guarded by _lock; _tripped uses
    ''' Interlocked so TripBreaker and ResetInternal are both compare-and-swap operations
    ''' — only one thread "wins" and logs the state change.
    ''' </summary>
    Friend Module NetworkBreaker
        ''' <summary>Number of DNS failures within <see cref="FailWindowSecs"/> that trips the breaker.</summary>
        Private Const FailThreshold As Integer = 5
        ''' <summary>Sliding window (seconds) over which failures are counted.</summary>
        Private Const FailWindowSecs As Double = 15.0
        ''' <summary>Seconds between DNS probe attempts while the breaker is tripped.</summary>
        Private Const ProbeIntervalSec As Integer = 30
        ''' <summary>Cancel the run if connectivity does not return within this many minutes.</summary>
        Private Const MaxWaitMinutes As Double = 15.0
        ''' <summary>Hostname used for the connectivity probe.</summary>
        Private Const ProbeHost As String = "www.google.com"

        Private ReadOnly _lock As New Object
        Private _failCount As Integer = 0
        Private _windowStart As Date = Date.MinValue
        Private _tripped As Integer = 0         ' 0 = open, 1 = tripped  (Interlocked)
        Private _tripTime As Date = Date.MinValue

        ''' <summary>
        ''' True while the breaker is tripped (connectivity lost).
        ''' Checked by <see cref="TDownloader.StartDownloading"/> between user batches.
        ''' </summary>
        Friend ReadOnly Property IsTripped As Boolean
            Get
                Return Interlocked.CompareExchange(_tripped, 0, 0) = 1
            End Get
        End Property

        ''' <summary>
        ''' Records one DNS resolution failure. Thread-safe; may be called concurrently
        ''' from multiple download tasks via <see cref="API.Base.UserDataBase.ProcessException"/>.
        ''' Trips the breaker if <see cref="FailThreshold"/> failures accumulate within the window.
        ''' </summary>
        Friend Sub RecordDnsFailure()
            Dim currentCount As Integer
            SyncLock _lock
                If (DateTime.Now - _windowStart).TotalSeconds > FailWindowSecs Then
                    _windowStart = DateTime.Now
                    _failCount = 1
                Else
                    _failCount += 1
                End If
                currentCount = _failCount
            End SyncLock
            If currentCount >= FailThreshold Then TripBreaker()
        End Sub

        ''' <summary>
        ''' Trips the breaker. CompareExchange ensures only the first thread to call this
        ''' (after the threshold is crossed) performs the trip and logs the message.
        ''' </summary>
        Private Sub TripBreaker()
            If Interlocked.CompareExchange(_tripped, 1, 0) = 0 Then
                _tripTime = DateTime.Now
                Downloader.Suspended = True
                MyMainLOG = $"Network connectivity lost — download paused. " &
                            $"Waiting up to {CInt(MaxWaitMinutes)} min for connection to return..."
                ActivityLog.Add($"network connectivity lost — downloads paused (waiting up to {CInt(MaxWaitMinutes)} min)")
            End If
        End Sub

        ''' <summary>
        ''' Resets the breaker to the open (normal) state. CompareExchange ensures only
        ''' the first thread to successfully reset logs the "restored" message.
        ''' </summary>
        Private Sub ResetInternal()
            If Interlocked.CompareExchange(_tripped, 0, 1) = 1 Then
                SyncLock _lock
                    _failCount = 0
                    _windowStart = Date.MinValue
                End SyncLock
                Downloader.Suspended = False
                MyMainLOG = "Network connectivity restored — resuming download."
                ActivityLog.Add("network connectivity restored — resuming downloads")
            End If
        End Sub

        ''' <summary>
        ''' Silently resets all state. Called at the start of each Job so a previous
        ''' run's tripped state (if the app wasn't restarted) does not carry over.
        ''' </summary>
        Friend Sub ResetSilent()
            SyncLock _lock
                _failCount = 0
                _windowStart = Date.MinValue
            End SyncLock
            Interlocked.Exchange(_tripped, 0)
            Downloader.Suspended = False
        End Sub

        ''' <summary>
        ''' Blocks the calling thread, probing DNS every <see cref="ProbeIntervalSec"/> seconds,
        ''' until connectivity is restored or the timeout expires.
        ''' </summary>
        ''' <param name="Token">Job cancellation token — honoured inside the wait loop.</param>
        ''' <returns>
        ''' <see langword="True"/> if connectivity was restored (caller should continue);
        ''' <see langword="False"/> if <see cref="MaxWaitMinutes"/> elapsed without recovery
        ''' (caller should stop the run).
        ''' </returns>
        Friend Function WaitForConnectivity(ByVal Token As CancellationToken) As Boolean
            Dim deadline As Date = _tripTime.AddMinutes(MaxWaitMinutes)
            Do
                Token.ThrowIfCancellationRequested()
                ' Probe before sleeping so a false-positive trip (a single service down while
                ' general connectivity is fine) recovers immediately instead of after a full
                ' ProbeIntervalSec delay.
                If CheckConnectivity() Then
                    ResetInternal()
                    Return True
                End If
                If DateTime.Now > deadline Then
                    MyMainLOG = $"Network connectivity did not return within " &
                                $"{CInt(MaxWaitMinutes)} min — download stopped."
                    ActivityLog.Add($"network connectivity did not return within {CInt(MaxWaitMinutes)} min — download stopped")
                    ' Clear the tripped state (and Downloader.Suspended) on give-up so it doesn't
                    ' linger after the run ends. ResetSilent doesn't log a misleading "restored".
                    ResetSilent()
                    Return False
                End If
                Token.ThrowIfCancellationRequested()
                Thread.Sleep(ProbeIntervalSec * 1000)
            Loop
        End Function

        ''' <summary>
        ''' Performs a lightweight DNS lookup to test whether general internet connectivity
        ''' has returned. Returns True on success, False on any failure.
        ''' </summary>
        Private Function CheckConnectivity() As Boolean
            Try
                Dns.GetHostEntry(ProbeHost)
                Return True
            Catch
                Return False
            End Try
        End Function
    End Module
End Namespace
