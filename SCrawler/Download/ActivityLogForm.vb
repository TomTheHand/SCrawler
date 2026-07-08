' Copyright (C) 2023  Andy https://github.com/AAndyProgram
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY
Imports System.ComponentModel
Imports PersonalUtilities.Forms
Namespace DownloadObjects
    ''' <summary>
    ''' Live viewer for <see cref="ActivityLog"/>: backfills from <see cref="ActivityLog.Snapshot"/>
    ''' when shown and follows <see cref="ActivityLog.EntryAdded"/> while visible
    ''' (the event arrives on producer threads — appends are marshalled via BeginInvoke).
    ''' Closing hides the form (same pattern as UserSearchForm); MainFrame disposes it on exit.
    ''' </summary>
    Friend Class ActivityLogForm : Inherits Form
        Private ReadOnly MyView As FormView
        Private ReadOnly LIST_LOG As ListBox
        Private ReadOnly BTT_AUTOSCROLL As ToolStripButton
        Private _Subscribed As Boolean = False
        Friend Sub New()
            Name = "ActivityLogForm"
            Text = "Activity log"
            KeyPreview = True
            StartPosition = FormStartPosition.CenterScreen
            Size = New Size(800, 450)
            MinimumSize = New Size(400, 200)

            LIST_LOG = New ListBox With {
                .Dock = DockStyle.Fill,
                .SelectionMode = SelectionMode.MultiExtended,
                .IntegralHeight = False,
                .HorizontalScrollbar = True,
                .Font = New Font("Consolas", 9)
            }
            BTT_AUTOSCROLL = New ToolStripButton("Autoscroll") With {.CheckOnClick = True, .Checked = True,
                .ToolTipText = "Automatically scroll to the newest entry."}
            Dim bttCopy As New ToolStripButton("Copy") With {.ToolTipText = "Copy the selected entries (or the entire log) to the clipboard."}
            AddHandler bttCopy.Click, Sub() CopyEntries()
            Dim bttClear As New ToolStripButton("Clear") With {.ToolTipText = "Clear the activity log."}
            AddHandler bttClear.Click, Sub()
                                           ActivityLog.Clear()
                                           LIST_LOG.Items.Clear()
                                       End Sub
            Dim tBar As New ToolStrip With {.GripStyle = ToolStripGripStyle.Hidden}
            tBar.Items.AddRange(New ToolStripItem() {BTT_AUTOSCROLL, New ToolStripSeparator, bttCopy, bttClear})

            Controls.Add(LIST_LOG)
            Controls.Add(tBar)

            MyView = New FormView(Me)
            MyView.Import(Settings.Design)
        End Sub
        Private Sub ActivityLogForm_Load(sender As Object, e As EventArgs) Handles Me.Load
            MyView.SetFormSize()
        End Sub
        Private Sub ActivityLogForm_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
            e.Cancel = True
            Hide()
        End Sub
        Private Sub ActivityLogForm_Disposed(sender As Object, e As EventArgs) Handles Me.Disposed
            Unsubscribe()
            MyView.Dispose()
        End Sub
        Private Sub ActivityLogForm_VisibleChanged(sender As Object, e As EventArgs) Handles Me.VisibleChanged
            If Visible Then
                Backfill()
                If Not _Subscribed Then _Subscribed = True : AddHandler ActivityLog.EntryAdded, AddressOf EntryAdded_Handler
            Else
                Unsubscribe()
            End If
        End Sub
        Private Sub ActivityLogForm_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
            If e.KeyCode = Keys.Escape Then
                Hide()
                e.Handled = True
            ElseIf e.Control And e.KeyCode = Keys.C Then
                CopyEntries()
                e.Handled = True
            ElseIf e.Control And e.KeyCode = Keys.A Then
                LIST_LOG.BeginUpdate()
                For i% = 0 To LIST_LOG.Items.Count - 1 : LIST_LOG.SetSelected(i, True) : Next
                LIST_LOG.EndUpdate()
                e.Handled = True
            End If
        End Sub
        Private Sub Unsubscribe()
            If _Subscribed Then _Subscribed = False : RemoveHandler ActivityLog.EntryAdded, AddressOf EntryAdded_Handler
        End Sub
        Private Sub Backfill()
            With LIST_LOG
                .BeginUpdate()
                .Items.Clear()
                .Items.AddRange(ActivityLog.Snapshot.ToArray)
                .EndUpdate()
                If .Items.Count > 0 Then .TopIndex = .Items.Count - 1
            End With
        End Sub
        ''' <summary>Raised on producer threads — marshal to the UI thread.</summary>
        Private Sub EntryAdded_Handler(ByVal Line As String)
            Try
                If IsHandleCreated AndAlso Not IsDisposed Then BeginInvoke(Sub() Append(Line))
            Catch
            End Try
        End Sub
        Private Sub Append(ByVal Line As String)
            Try
                With LIST_LOG
                    While .Items.Count >= ActivityLog.MaxEntries : .Items.RemoveAt(0) : End While
                    .Items.Add(Line)
                    If BTT_AUTOSCROLL.Checked Then .TopIndex = .Items.Count - 1
                End With
            Catch
            End Try
        End Sub
        Private Sub CopyEntries()
            Try
                Dim l As New List(Of String)
                With LIST_LOG
                    If .SelectedIndices.Count > 0 Then
                        For Each i% In .SelectedIndices : l.Add(.Items(i).ToString) : Next
                    Else
                        For Each item In .Items : l.Add(item.ToString) : Next
                    End If
                End With
                If l.Count > 0 Then Clipboard.SetText(String.Join(vbNewLine, l))
            Catch
            End Try
        End Sub
    End Class
End Namespace
