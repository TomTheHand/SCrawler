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
    ''' </summary>
    Friend Module ActivityLog
        ''' <summary>Rolling-buffer cap; oldest entries are dropped beyond this.</summary>
        Friend Const MaxEntries As Integer = 5000

        Private ReadOnly _lock As New Object
        Private ReadOnly _entries As New List(Of String)

        ''' <summary>Raised for every new line, on the producer's thread.</summary>
        Friend Event EntryAdded(ByVal Line As String)

        Friend Sub Add(ByVal Text As String)
            Dim line$ = $"{Now:HH:mm:ss} {Text}"
            SyncLock _lock
                _entries.Add(line)
                If _entries.Count > MaxEntries Then _entries.RemoveRange(0, _entries.Count - MaxEntries)
            End SyncLock
            Try : RaiseEvent EntryAdded(line) : Catch : End Try
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
