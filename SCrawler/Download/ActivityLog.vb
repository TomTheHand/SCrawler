' Copyright (C) 2023  Andy https://github.com/AAndyProgram
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY
Namespace DownloadObjects
    ''' <summary>
    ''' Live feed of downloader activity: which job/user is being processed right now,
    ''' how many files are queued, and per-file progress. Unlike <c>MyMainLOG</c> (the
    ''' error log), this is a high-volume rolling buffer meant for a live viewer window.
    '''
    ''' Producers are the download threads (TDownloader jobs, per-user tasks inside
    ''' UserDataBase.DownloadContentDefault), so <see cref="Add"/> must be cheap and
    ''' thread-safe. Consumers subscribe to <see cref="EntryAdded"/> for live updates
    ''' (the event is raised on the producer thread — UI subscribers must marshal via
    ''' BeginInvoke) and call <see cref="Snapshot"/> to backfill on open.
    '''
    ''' Every line is also mirrored to <c>LOGs\Activity_*.txt</c>. The in-memory buffer is lost when the
    ''' program closes, which is precisely when it is most wanted: the questions this log answers —
    ''' "what was it doing during that long gap", "did it stall or was it pacing" — are asked after a run
    ''' has ended, or after a hang, and the answer used to die with the process.
    ''' </summary>
    Friend Module ActivityLog
        ''' <summary>Rolling-buffer cap; oldest entries are dropped beyond this. The file keeps everything.</summary>
        Friend Const MaxEntries As Integer = 5000
        ''' <summary>How many past run logs to keep on disk.</summary>
        Private Const KeepRunLogs As Integer = 10

        Private ReadOnly _lock As New Object
        Private ReadOnly _entries As New List(Of String)
        Private _writer As IO.StreamWriter = Nothing
        Private _writerReady As Boolean = False
        Private _writerFailed As Boolean = False

        ''' <summary>Raised for every new line, on the producer's thread.</summary>
        Friend Event EntryAdded(ByVal Line As String)

        ''' <summary>
        ''' Opens this run's log file on first use. AutoFlush is on deliberately: the log's whole purpose
        ''' is diagnosing hangs and crashes, and a buffered tail would be lost in exactly those cases.
        ''' Any failure disables file logging permanently rather than disturbing a download.
        ''' Caller must hold <c>_lock</c>.
        ''' </summary>
        Private Sub EnsureWriter()
            If _writerReady OrElse _writerFailed Then Exit Sub
            _writerReady = True
            Try
                Dim dir$ = $"{My.Application.Info.DirectoryPath}\LOGs"
                If Not IO.Directory.Exists(dir) Then IO.Directory.CreateDirectory(dir)
                ' Prune old run logs before opening the new one.
                Try
                    Dim old = IO.Directory.GetFiles(dir, "Activity_*.txt").OrderByDescending(Function(p) p).Skip(KeepRunLogs)
                    For Each p$ In old : IO.File.Delete(p) : Next
                Catch
                End Try
                _writer = New IO.StreamWriter($"{dir}\Activity_{Now:yyyyMMdd_HHmmss}.txt", False, New Text.UTF8Encoding(True)) With {.AutoFlush = True}
                _writer.WriteLine($"# SCrawler activity log — started {Now:yyyy-MM-dd HH:mm:ss}")
            Catch
                _writerFailed = True
                _writer = Nothing
            End Try
        End Sub

        Friend Sub Add(ByVal Text As String)
            Dim line$ = $"{Now:HH:mm:ss} {Text}"
            SyncLock _lock
                _entries.Add(line)
                If _entries.Count > MaxEntries Then _entries.RemoveRange(0, _entries.Count - MaxEntries)
                EnsureWriter()
                If Not _writer Is Nothing Then
                    Try
                        _writer.WriteLine(line)
                    Catch
                        ' Never let logging break a download; stop trying after the first failure.
                        _writerFailed = True
                        Try : _writer.Dispose() : Catch : End Try
                        _writer = Nothing
                    End Try
                End If
            End SyncLock
            Try : RaiseEvent EntryAdded(line) : Catch : End Try
        End Sub

        ''' <summary>Full path of this run's log file, or empty if file logging is not active.</summary>
        Friend ReadOnly Property LogFile As String
            Get
                SyncLock _lock
                    Try
                        If Not _writer Is Nothing AndAlso TypeOf _writer.BaseStream Is IO.FileStream Then _
                           Return DirectCast(_writer.BaseStream, IO.FileStream).Name
                    Catch
                    End Try
                    Return String.Empty
                End SyncLock
            End Get
        End Property

        ''' <summary>Closes this run's log file. Called when the program shuts down.</summary>
        Friend Sub CloseFile()
            SyncLock _lock
                Try
                    If Not _writer Is Nothing Then
                        _writer.WriteLine($"# closed {Now:yyyy-MM-dd HH:mm:ss}")
                        _writer.Flush()
                        _writer.Dispose()
                    End If
                Catch
                End Try
                _writer = Nothing
                _writerFailed = True   'do not reopen during shutdown
            End SyncLock
        End Sub

        ''' <summary>Copy of the current buffer (oldest first).</summary>
        Friend Function Snapshot() As List(Of String)
            SyncLock _lock
                Return New List(Of String)(_entries)
            End SyncLock
        End Function

        Friend Sub Clear()
            SyncLock _lock
                _entries.Clear()
            End SyncLock
        End Sub
    End Module
End Namespace
