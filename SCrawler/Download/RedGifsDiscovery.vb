' Copyright (C) 2023  Andy https://github.com/AAndyProgram
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY
Imports SCrawler.API
Imports SCrawler.API.Base
Imports PersonalUtilities.Functions.XML
Imports PersonalUtilities.Functions.XML.Base
Namespace DownloadObjects
    ''' <summary>
    ''' Persistent record of RedGifs creator accounts noticed in Reddit users' posts.
    '''
    ''' Reddit's <c>ReparseVideo</c> resolves every RedGifs link through the RedGifs gif API, whose
    ''' response names the creator — so discovery costs no extra requests (see
    ''' <c>RedGifs.UserData.GetDataFromUrlId</c>). Entries accumulate here across runs so they can be
    ''' reviewed whenever the user feels like it, and so a rejected suggestion stays rejected: a RedGifs
    ''' link does NOT prove the Reddit poster owns the account (crossposts and reposts name other
    ''' creators), which is why nothing is ever added automatically.
    '''
    ''' Entries are keyed on (RedGifs account + Reddit user): the same creator discovered under two
    ''' different Reddit users is two suggestions, because accepting one says nothing about the other.
    ''' </summary>
    Friend Module RedGifsDiscovery
        Private ReadOnly DiscoveryFile As SFile = $"{SettingsFolderName}\RedGifsDiscoveries.xml"
        Friend Structure Discovery : Implements IEContainerProvider, IEquatable(Of Discovery)
            Private Const Name_Node As String = "Discovery"
            Private Const Name_RedditUser As String = "RedditUser"
            Private Const Name_RedditKey As String = "RedditKey"
            Private Const Name_Count As String = "Count"
            Private Const Name_Dismissed As String = "Dismissed"
            ''' <summary>The discovered RedGifs account name.</summary>
            Friend RedGifsName As String
            ''' <summary>Display name of the Reddit user whose posts linked it.</summary>
            Friend RedditUser As String
            ''' <summary>Key of that Reddit user, for looking the live object back up.</summary>
            Friend RedditKey As String
            ''' <summary>How many of that user's RedGifs posts named this account (evidence of ownership).</summary>
            Friend Count As Integer
            ''' <summary>User rejected this suggestion; keep it so it stops being offered.</summary>
            Friend Dismissed As Boolean
            Friend Sub New(ByVal _RedGifsName As String, ByVal _RedditUser As String, ByVal _RedditKey As String, ByVal _Count As Integer)
                RedGifsName = _RedGifsName
                RedditUser = _RedditUser
                RedditKey = _RedditKey
                Count = _Count
                Dismissed = False
            End Sub
            Private Sub New(ByVal e As EContainer)
                RedGifsName = e.Value
                RedditUser = e.Attribute(Name_RedditUser).Value
                RedditKey = e.Attribute(Name_RedditKey).Value
                Count = e.Attribute(Name_Count).Value.FromXML(Of Integer)(0)
                Dismissed = e.Attribute(Name_Dismissed).Value.FromXML(Of Boolean)(False)
            End Sub
            Public Shared Widening Operator CType(ByVal e As EContainer) As Discovery
                Return New Discovery(e)
            End Operator
            Private Function ToEContainer(Optional ByVal e As ErrorsDescriber = Nothing) As EContainer Implements IEContainerProvider.ToEContainer
                Return New EContainer(Name_Node, RedGifsName, {New EAttribute(Name_RedditUser, RedditUser),
                                                               New EAttribute(Name_RedditKey, RedditKey),
                                                               New EAttribute(Name_Count, Count),
                                                               New EAttribute(Name_Dismissed, Dismissed)})
            End Function
            Friend Overloads Function Equals(ByVal Other As Discovery) As Boolean Implements IEquatable(Of Discovery).Equals
                Return String.Equals(RedGifsName, Other.RedGifsName, StringComparison.OrdinalIgnoreCase) AndAlso
                       String.Equals(RedditKey, Other.RedditKey, StringComparison.OrdinalIgnoreCase)
            End Function
            Public Overrides Function Equals(ByVal Obj As Object) As Boolean
                Return Not IsNothing(Obj) AndAlso TypeOf Obj Is Discovery AndAlso Equals(DirectCast(Obj, Discovery))
            End Function
            Public Overrides Function GetHashCode() As Integer
                Return $"{RedGifsName}|{RedditKey}".ToLower.GetHashCode
            End Function
        End Structure

        Private ReadOnly MyLock As New Object
        Private ReadOnly Items As New List(Of Discovery)
        Private Loaded As Boolean = False
        Private NewSinceReview As Boolean = False
        ''' <summary>Raised when the stored set changes, so an open viewer can refresh.</summary>
        Friend Event Changed()
        ''' <summary>
        ''' A previously unseen suggestion has been recorded since the viewer was last opened — used to
        ''' decide whether a completed run should surface the reviewer. Not persisted: a restart simply
        ''' means the next run decides again, which is preferable to nagging at startup.
        ''' </summary>
        Friend ReadOnly Property HasNewSinceReview As Boolean
            Get
                Return NewSinceReview AndAlso Pending().Count > 0
            End Get
        End Property
        ''' <summary>Called when the reviewer is opened, so the user is not shown it again unprompted.</summary>
        Friend Sub MarkReviewed()
            NewSinceReview = False
        End Sub

        Friend Sub Load()
            SyncLock MyLock
                If Loaded Then Exit Sub
                Loaded = True
                Try
                    If DiscoveryFile.Exists Then
                        Using x As New XmlFile(DiscoveryFile, Protector.Modes.All, False) With {.AllowSameNames = True, .XmlReadOnly = True}
                            x.LoadData()
                            Items.ListAddList(x, LAP.IgnoreICopier)
                        End Using
                    End If
                Catch ex As Exception
                    ErrorsDescriber.Execute(EDP.SendToLog, ex, "RedGifsDiscovery.Load")
                End Try
            End SyncLock
        End Sub
        Private Sub SaveInternal()
            Try
                If Items.Count > 0 Then
                    Using x As New XmlFile With {.AllowSameNames = True, .Name = "RedGifsDiscoveries"}
                        x.AddRange(Items)
                        x.Save(DiscoveryFile)
                    End Using
                ElseIf DiscoveryFile.Exists Then
                    DiscoveryFile.Delete(SFO.File, SFODelete.DeletePermanently, EDP.ReturnValue)
                End If
            Catch ex As Exception
                ErrorsDescriber.Execute(EDP.SendToLog, ex, "RedGifsDiscovery.Save")
            End Try
        End Sub

        ''' <summary>
        ''' Records (or refreshes) a discovery. The count is the number of the Reddit user's RedGifs posts
        ''' naming this account in the run that found it; the highest seen is kept, since incremental runs
        ''' only look at new posts and would otherwise keep overwriting a strong signal with a weak one.
        ''' A dismissed entry stays dismissed.
        ''' </summary>
        Friend Sub Record(ByVal RedGifsName As String, ByVal RedditUser As String, ByVal RedditKey As String, ByVal Count As Integer)
            If RedGifsName.IsEmptyString Then Exit Sub
            Load()
            Dim changed As Boolean = False
            SyncLock MyLock
                Dim d As New Discovery(RedGifsName, RedditUser, RedditKey, Count)
                Dim i% = Items.IndexOf(d)
                If i >= 0 Then
                    Dim existing As Discovery = Items(i)
                    If Count > existing.Count Then
                        existing.Count = Count
                        existing.RedditUser = RedditUser
                        Items(i) = existing
                        changed = True
                    End If
                Else
                    Items.Add(d)
                    changed = True
                    NewSinceReview = True
                End If
                If changed Then SaveInternal()
            End SyncLock
            If changed Then RaiseEvent Changed()
        End Sub
        ''' <summary>All stored discoveries, newest information first.</summary>
        Friend Function Snapshot() As List(Of Discovery)
            Load()
            SyncLock MyLock : Return New List(Of Discovery)(Items) : End SyncLock
        End Function
        ''' <summary>
        ''' Suggestions actually worth showing: not dismissed, and the RedGifs account is not already in
        ''' SCrawler (adding it is the whole point, so an existing one is nothing to suggest).
        ''' </summary>
        Friend Function Pending() As List(Of Discovery)
            Return Snapshot().Where(Function(d) Not d.Dismissed AndAlso Not RedGifsUserExists(d.RedGifsName)).
                              OrderByDescending(Function(d) d.Count).ToList
        End Function
        ''' <summary>Marks a suggestion rejected so it is not offered again.</summary>
        Friend Sub Dismiss(ByVal Item As Discovery)
            SetDismissed(Item, True)
        End Sub
        ''' <summary>Un-rejects a suggestion.</summary>
        Friend Sub Restore(ByVal Item As Discovery)
            SetDismissed(Item, False)
        End Sub
        Private Sub SetDismissed(ByVal Item As Discovery, ByVal Value As Boolean)
            Load()
            Dim changed As Boolean = False
            SyncLock MyLock
                Dim i% = Items.IndexOf(Item)
                If i >= 0 AndAlso Not Items(i).Dismissed = Value Then
                    Dim d As Discovery = Items(i)
                    d.Dismissed = Value
                    Items(i) = d
                    changed = True
                    SaveInternal()
                End If
            End SyncLock
            If changed Then RaiseEvent Changed()
        End Sub
        ''' <summary>Drops an entry entirely (used once a suggestion has been acted on).</summary>
        Friend Sub Forget(ByVal Item As Discovery)
            Load()
            Dim changed As Boolean = False
            SyncLock MyLock
                Dim i% = Items.IndexOf(Item)
                If i >= 0 Then Items.RemoveAt(i) : changed = True : SaveInternal()
            End SyncLock
            If changed Then RaiseEvent Changed()
        End Sub
        ''' <summary>Is a RedGifs user with this name already added to SCrawler?</summary>
        Friend Function RedGifsUserExists(ByVal UserName As String) As Boolean
            Try
                Return Settings.UsersList.Exists(Function(u) u.Plugin = RedGifs.RedGifsSiteKey AndAlso
                                                             String.Equals(u.Name, UserName, StringComparison.OrdinalIgnoreCase))
            Catch
                Return False
            End Try
        End Function
    End Module
End Namespace
