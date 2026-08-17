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
Imports UStates = SCrawler.API.Base.UserMedia.States
Imports UTypes = SCrawler.API.Base.UserMedia.Types
Namespace DownloadObjects
    ''' <summary>
    ''' Removes media that arrives twice through two accounts held in the same collection.
    '''
    ''' Reddit posters commonly link videos hosted on their own RedGifs account. With both accounts in
    ''' one SCrawler collection the same video is downloaded twice: once under the Reddit user (whose
    ''' <c>ReparseVideo</c> resolves the link to the RedGifs CDN) and once under the RedGifs user.
    '''
    ''' The RedGifs copy is treated as canonical — it carries the RedGifs post date and sits under the
    ''' account that actually produced the video — so the Reddit-side copy is the one removed.
    '''
    ''' Matching is by RedGifs gif ID, not by file hash, for two reasons: RedGifs media is
    ''' <see cref="UTypes.Video"/>, which <c>UserDataBase.DownloadContentDefault</c> deliberately
    ''' excludes from MD5 comparison (only GIF/Picture are hashed), and the ID is exact and free —
    ''' both sides already store it (RedGifs as <c>Post.ID</c>, Reddit inside <c>URL_BASE</c>).
    '''
    ''' Runs once after a job finishes rather than during downloading, so it does not depend on the
    ''' order in which collection members happen to download. A gif that is on Reddit but NOT on the
    ''' creator's RedGifs timeline simply has no match and is kept under the Reddit user. Files are
    ''' sent to the recycle bin, never permanently deleted.
    ''' </summary>
    Friend Module CrossAccountDedup
        ''' <summary>Sweeps every collection for Reddit/RedGifs duplicates. Safe to call when idle.</summary>
        Friend Sub RemoveRedGifsDuplicates()
            Try
                If Settings.Users.Count = 0 Then Exit Sub
                For Each u As IUserData In Settings.Users.Where(Function(uu) uu.IsCollection).ToList
                    If TypeOf u Is UserDataBind Then ProcessCollection(DirectCast(u, UserDataBind))
                Next
            Catch ex As Exception
                ErrorsDescriber.Execute(EDP.SendToLog, ex, "CrossAccountDedup.RemoveRedGifsDuplicates")
            End Try
        End Sub
        Private Sub ProcessCollection(ByVal Collection As UserDataBind)
            Try
                If Collection.Count < 2 Then Exit Sub

                ' Members by site. Both sides must be present for a duplicate to be possible.
                Dim redGifsUsers As List(Of UserDataBase) = MembersOfSite(Collection, RedGifs.RedGifsSiteKey)
                Dim redditUsers As List(Of UserDataBase) = MembersOfSite(Collection, Reddit.RedditSiteKey)
                If redGifsUsers.Count = 0 Or redditUsers.Count = 0 Then Exit Sub

                ' Every gif ID the RedGifs side of this collection holds. RedGifs stores the gif ID
                ' directly as the post ID (see RedGifs.UserData.GetDataFromUrlId).
                Dim ownedIds As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                For Each rgUser As UserDataBase In redGifsUsers
                    For Each m As UserMedia In rgUser.ContentSnapshot
                        If Not m.Post.ID.IsEmptyString Then ownedIds.Add(m.Post.ID)
                    Next
                Next
                If ownedIds.Count = 0 Then Exit Sub

                For Each redditUser As UserDataBase In redditUsers
                    Dim removed% = redditUser.RemoveContentAndRecycleFiles(Function(m) IsDuplicateOfOwnedGif(m, ownedIds))
                    If removed > 0 Then _
                       ActivityLog.Add($"[{redditUser.Site}] {redditUser.Name}: {removed} RedGifs duplicate(s) " &
                                       $"recycled — kept under the RedGifs account in collection [{Collection.CollectionName}]")
                Next
            Catch ex As Exception
                ErrorsDescriber.Execute(EDP.SendToLog, ex, $"CrossAccountDedup.ProcessCollection({Collection.CollectionName})")
            End Try
        End Sub
        ''' <summary>Collection members belonging to <paramref name="SiteKey"/> (skips nested/disposed entries).</summary>
        Private Function MembersOfSite(ByVal Collection As UserDataBind, ByVal SiteKey As String) As List(Of UserDataBase)
            Dim result As New List(Of UserDataBase)
            For Each u As IUserData In Collection.Collections
                If Not u Is Nothing AndAlso Not u.Disposed AndAlso Not u.IsCollection AndAlso
                   TypeOf u Is UserDataBase AndAlso If(u.HOST?.Key, String.Empty) = SiteKey Then result.Add(DirectCast(u, UserDataBase))
            Next
            Return result
        End Function
        ''' <summary>
        ''' Is this Reddit-side item a RedGifs video the collection's RedGifs account already holds?
        ''' Only downloaded items are considered — a Missing record has no file to recycle, and dropping
        ''' it would lose the retry budget that may still recover it.
        ''' </summary>
        Private Function IsDuplicateOfOwnedGif(ByVal Media As UserMedia, ByVal OwnedIds As HashSet(Of String)) As Boolean
            If Not Media.State = UStates.Downloaded Then Return False
            ' URL_BASE holds the original redgifs watch URL (set in Reddit.UserData.ReparseVideo);
            ' URL is the resolved CDN link, which also carries the ID. Try both.
            Dim id$ = RedGifs.UserData.GetVideoIdFromUrl(Media.URL_BASE)
            If id.IsEmptyString Then id = RedGifs.UserData.GetVideoIdFromUrl(Media.URL)
            Return Not id.IsEmptyString AndAlso OwnedIds.Contains(id)
        End Function
    End Module
End Namespace
