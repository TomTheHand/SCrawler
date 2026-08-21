' Copyright (C) 2023  Andy https://github.com/AAndyProgram
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY
Imports System.ComponentModel
Imports SCrawler.API
Imports SCrawler.API.Base
Imports SCrawler.Plugin.Hosts
Imports PersonalUtilities.Forms
Imports Discovery = SCrawler.DownloadObjects.RedGifsDiscovery.Discovery
Namespace DownloadObjects
    ''' <summary>
    ''' Reviewer for <see cref="RedGifsDiscovery"/>: RedGifs creator accounts noticed in Reddit users'
    ''' posts, with the evidence (how many of that user's RedGifs posts named the account) so a
    ''' crosspost-sourced false positive is easy to spot and reject.
    '''
    ''' Accepting one creates the RedGifs user, then hands off to the main window's own
    ''' <c>AddSelectedUsersToCollection</c> with the right users selected — so the collection is created
    ''' (or joined) by SCrawler's existing code, including its folder relocation and usage-model prompts,
    ''' rather than anything reimplemented here.
    ''' </summary>
    Friend Class RedGifsDiscoveryForm : Inherits Form
        Private ReadOnly MyView As FormView
        Private ReadOnly LIST_ITEMS As ListView
        Private ReadOnly BTT_SHOW_DISMISSED As ToolStripButton
        Private ReadOnly LBL_STATUS As ToolStripLabel
        Private _Subscribed As Boolean = False
        Private _Refilling As Boolean = False
        Friend Sub New()
            Name = "RedGifsDiscoveryForm"
            Text = "Discovered RedGifs accounts"
            KeyPreview = True
            StartPosition = FormStartPosition.CenterScreen
            Size = New Size(860, 420)
            MinimumSize = New Size(560, 260)

            LIST_ITEMS = New ListView With {
                .Dock = DockStyle.Fill,
                .View = View.Details,
                .CheckBoxes = True,
                .FullRowSelect = True,
                .HideSelection = False,
                .MultiSelect = True
            }
            With LIST_ITEMS.Columns
                .Add("RedGifs account", 200)
                .Add("Seen in posts by", 190)
                .Add("Posts", 60, HorizontalAlignment.Right)
                .Add("Collection", 170)
                .Add("Status", 110)
            End With

            Dim bttAdd As New ToolStripButton("Add checked") With {
                .ToolTipText = "Create the checked RedGifs accounts and put each one in a collection with the Reddit user it was found under."}
            AddHandler bttAdd.Click, Sub() AddChecked()
            Dim bttDismiss As New ToolStripButton("Dismiss checked") With {
                .ToolTipText = "Reject the checked suggestions so they stop being offered (e.g. creators that came from crossposts)."}
            AddHandler bttDismiss.Click, Sub() DismissChecked(True)
            Dim bttRestore As New ToolStripButton("Restore checked") With {
                .ToolTipText = "Un-reject the checked suggestions."}
            AddHandler bttRestore.Click, Sub() DismissChecked(False)
            BTT_SHOW_DISMISSED = New ToolStripButton("Show dismissed") With {.CheckOnClick = True,
                .ToolTipText = "Also list suggestions you have already rejected."}
            AddHandler BTT_SHOW_DISMISSED.Click, Sub() Refill()
            LBL_STATUS = New ToolStripLabel(String.Empty)

            Dim tBar As New ToolStrip With {.GripStyle = ToolStripGripStyle.Hidden}
            tBar.Items.AddRange(New ToolStripItem() {bttAdd, New ToolStripSeparator, bttDismiss, bttRestore,
                                                     New ToolStripSeparator, BTT_SHOW_DISMISSED, New ToolStripSeparator, LBL_STATUS})

            Controls.Add(LIST_ITEMS)
            Controls.Add(tBar)

            MyView = New FormView(Me)
            MyView.Import(Settings.Design)
        End Sub
        Private Sub RedGifsDiscoveryForm_Load(sender As Object, e As EventArgs) Handles Me.Load
            MyView.SetFormSize()
        End Sub
        Private Sub RedGifsDiscoveryForm_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
            e.Cancel = True
            Hide()
        End Sub
        Private Sub RedGifsDiscoveryForm_Disposed(sender As Object, e As EventArgs) Handles Me.Disposed
            Unsubscribe()
            MyView.Dispose()
        End Sub
        Private Sub RedGifsDiscoveryForm_VisibleChanged(sender As Object, e As EventArgs) Handles Me.VisibleChanged
            If Visible Then
                RedGifsDiscovery.MarkReviewed()
                Refill()
                If Not _Subscribed Then _Subscribed = True : AddHandler RedGifsDiscovery.Changed, AddressOf Discovery_Changed
            Else
                Unsubscribe()
            End If
        End Sub
        Private Sub RedGifsDiscoveryForm_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
            If e.KeyCode = Keys.Escape Then Hide() : e.Handled = True
        End Sub
        Private Sub Unsubscribe()
            If _Subscribed Then _Subscribed = False : RemoveHandler RedGifsDiscovery.Changed, AddressOf Discovery_Changed
        End Sub
        ''' <summary>May arrive from a download thread — marshal before touching the list.</summary>
        Private Sub Discovery_Changed()
            Try
                If IsHandleCreated AndAlso Not IsDisposed AndAlso Visible Then BeginInvoke(Sub() Refill())
            Catch
            End Try
        End Sub
#Region "List"
        Private Sub Refill()
            Try
                _Refilling = True
                Dim showDismissed As Boolean = BTT_SHOW_DISMISSED.Checked
                Dim items As List(Of Discovery) = RedGifsDiscovery.Snapshot().
                    Where(Function(d) (showDismissed OrElse Not d.Dismissed) AndAlso
                                      Not RedGifsDiscovery.RedGifsUserExists(d.RedGifsName)).
                    OrderBy(Function(d) d.Dismissed).ThenByDescending(Function(d) d.Count).ToList
                With LIST_ITEMS
                    .BeginUpdate()
                    .Items.Clear()
                    For Each d As Discovery In items
                        Dim lvi As New ListViewItem(d.RedGifsName) With {.Tag = d}
                        lvi.SubItems.Add(d.RedditUser)
                        lvi.SubItems.Add(d.Count.ToString)
                        lvi.SubItems.Add(CollectionNameOf(d).IfNullOrEmpty("(none)"))
                        lvi.SubItems.Add(IIf(d.Dismissed, "Dismissed", "New"))
                        .Items.Add(lvi)
                    Next
                    .EndUpdate()
                End With
                LBL_STATUS.Text = $"{items.Where(Function(d) Not d.Dismissed).Count} suggestion(s)"
            Catch ex As Exception
                ErrorsDescriber.Execute(EDP.SendToLog, ex, "RedGifsDiscoveryForm.Refill")
            Finally
                _Refilling = False
            End Try
        End Sub
        Private Function CheckedItems() As List(Of Discovery)
            Dim l As New List(Of Discovery)
            For Each lvi As ListViewItem In LIST_ITEMS.CheckedItems
                If TypeOf lvi.Tag Is Discovery Then l.Add(DirectCast(lvi.Tag, Discovery))
            Next
            Return l
        End Function
        Private Sub DismissChecked(ByVal Dismiss As Boolean)
            If _Refilling Then Exit Sub
            Dim items As List(Of Discovery) = CheckedItems()
            If items.Count = 0 Then MsgBoxE({"Nothing is checked.", Text}, vbExclamation) : Exit Sub
            For Each d As Discovery In items
                If Dismiss Then RedGifsDiscovery.Dismiss(d) Else RedGifsDiscovery.Restore(d)
            Next
            Refill()
        End Sub
#End Region
#Region "Accept"
        ''' <summary>Collection the Reddit user of this suggestion belongs to, or empty when standalone.</summary>
        Private Function CollectionNameOf(ByVal Item As Discovery) As String
            Try
                Dim u As IUserData = Settings.GetUser(Item.RedditKey, True)
                If Not u Is Nothing AndAlso TypeOf u Is UserDataBase Then Return DirectCast(u, UserDataBase).User.CollectionName
            Catch
            End Try
            Return String.Empty
        End Function
        Private Sub AddChecked()
            If _Refilling Then Exit Sub
            Dim items As List(Of Discovery) = CheckedItems()
            If items.Count = 0 Then MsgBoxE({"Nothing is checked.", Text}, vbExclamation) : Exit Sub
            If Settings.CollectionsPath.Value.IsEmptyString Then
                MsgBoxE({"Collection path not specified — set it in the settings before adding accounts to collections.", Text}, vbCritical)
                Exit Sub
            End If
            If MsgBoxE({$"{items.Count} account(s) will be created and put into a collection with the Reddit user each was found under." & vbCr &
                        "A new collection is named after that Reddit user; if the Reddit user is already in a collection, " &
                        "the account joins that one instead." & vbCr & vbCr &
                        "Note: creating a NEW collection moves the Reddit user's existing download folder.",
                        Text}, vbQuestion,,, {"Continue", "Cancel"}) = 1 Then Exit Sub
            Dim done% = 0
            For i% = 0 To items.Count - 1
                If AcceptOne(items(i), i + 1, items.Count) Then done += 1
            Next
            Refill()
            MsgBoxE({$"{done} of {items.Count} account(s) added.", Text}, IIf(done = items.Count, vbInformation, vbExclamation))
        End Sub
        ''' <summary>
        ''' Creates the RedGifs user, then drives the main window's add-to-collection command with the
        ''' RedGifs user plus either the Reddit user's collection (join it) or the Reddit user itself
        ''' (new collection). Returns True when the account was created.
        ''' </summary>
        Private Function AcceptOne(ByVal Item As Discovery, ByVal Index As Integer, ByVal Total As Integer) As Boolean
            Try
                Dim created As IUserData = CreateRedGifsUser(Item.RedGifsName)
                If created Is Nothing Then
                    MsgBoxE({$"Could not create the RedGifs user [{Item.RedGifsName}].", Text}, vbCritical)
                    Return False
                End If

                ' Prefer the Reddit user's collection when it has one: the collection is what appears in
                ' the profile list, not its members, so that is what must be selected to join it.
                Dim partnerKey$ = Item.RedditKey
                Dim colName$ = CollectionNameOf(Item)
                If Not colName.IsEmptyString Then
                    Dim col As IUserData = Settings.Users.Find(Function(u) u.IsCollection AndAlso u.CollectionName = colName)
                    If Not col Is Nothing Then partnerKey = col.Key
                End If

                ActivityLog.Add($"RedGifs account [{Item.RedGifsName}] added from discovery under [{Item.RedditUser}]")

                If MainFrameObj.MF.SelectUsers({partnerKey, created.Key}) >= 2 Then
                    ' Name a new collection after the Reddit user and skip the chooser: the pairing is
                    ' already decided by accepting the suggestion, and being asked to name it — after the
                    ' entry has left this list — meant having to remember the Reddit name. When the Reddit
                    ' user is already in a collection, that collection wins and this is ignored.
                    ' Context/suggestion are still passed for the fallback case where no name can be derived.
                    Dim ctx$ = $"{Item.RedditUser} + {Item.RedGifsName}"
                    If Total > 1 Then ctx = $"{Index} of {Total}: {ctx}"
                    Dim newColName$ = If(Item.RedditUser, String.Empty).StringRemoveWinForbiddenSymbols.StringTrim
                    MainFrameObj.MF.AddSelectedUsersToCollection(ctx, newColName, newColName)
                Else
                    MsgBoxE({$"RedGifs user [{Item.RedGifsName}] was created, but the users could not be selected " &
                             "automatically — add them to a collection manually.", Text}, vbExclamation)
                End If
                RedGifsDiscovery.Forget(Item)
                Return True
            Catch ex As Exception
                ErrorsDescriber.Execute(EDP.SendToLog, ex, $"RedGifsDiscoveryForm.AcceptOne({Item.RedGifsName})")
                Return False
            End Try
        End Function
        ''' <summary>
        ''' Adds a RedGifs user to SCrawler, mirroring <c>MainFrame.BTT_ADD_USER_Click</c>'s add path
        ''' (UserCreatorForm cannot be used programmatically — its <c>TryCreate</c> ignores the URL it is
        ''' given and reads the clipboard instead). Returns the existing user if it is already present.
        ''' </summary>
        Private Function CreateRedGifsUser(ByVal UserName As String) As IUserData
            Try
                Dim existing As IUserData = Settings.GetUsers(Function(eu) Not eu.IsCollection AndAlso
                                                                           If(eu.HOST?.Key, String.Empty) = RedGifs.RedGifsSiteKey AndAlso
                                                                           String.Equals(eu.Name, UserName, StringComparison.OrdinalIgnoreCase)).ListIfNothing.FirstOrDefault
                If Not existing Is Nothing Then Return existing

                Dim host As SettingsHost = Settings(RedGifs.RedGifsSiteKey).Default
                If host Is Nothing Then Return Nothing

                Dim u As New UserInfo(UserName, host)
                Settings.UpdateUsersList(u)
                Settings.Users.Add(UserDataBase.GetInstance(u))
                With Settings.Users.Last
                    If Not .FileExists Then
                        Settings.Labels.Add(LabelsKeeper.NoParsedUser)
                        .Self.Labels.ListAddValue(LabelsKeeper.NoParsedUser)
                        .UpdateUserInformation()
                    End If
                    Return .Self
                End With
            Catch ex As Exception
                ErrorsDescriber.Execute(EDP.SendToLog, ex, $"RedGifsDiscoveryForm.CreateRedGifsUser({UserName})")
                Return Nothing
            End Try
        End Function
#End Region
    End Class
End Namespace
