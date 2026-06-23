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
Imports SCrawler.API.Base
Imports SCrawler.API.YouTube.Objects
Imports PersonalUtilities.Functions.XML
Imports PersonalUtilities.Functions.RegularExpressions
Imports PersonalUtilities.Tools.Web.Clients
Imports PersonalUtilities.Tools.Web.Documents.JSON
Imports UTypes = SCrawler.API.Base.UserMedia.Types
Imports UStates = SCrawler.API.Base.UserMedia.States
Namespace API.RedGifs
    Friend Class UserData : Inherits UserDataBase
        Friend Const DataGone As HttpStatusCode = HttpStatusCode.Gone
        Private Const PostDataUrl As String = "https://api.redgifs.com/v2/gifs/{0}?views=yes&users=yes"
#Region "Base declarations"
        Private ReadOnly Property MySettings As SiteSettings
            Get
                Return DirectCast(HOST.Source, SiteSettings)
            End Get
        End Property
        Protected Overrides Sub LoadUserInformation_OptionalFields(ByRef Container As XmlFile, ByVal Loading As Boolean)
        End Sub
#End Region
#Region "Initializer"
        Friend Sub New()
            UseResponserClient = True
        End Sub
#End Region
#Region "Download functions"
        ' Entry point for all RedGifs user downloads.
        '
        ' Token management: RedGifs uses temporary bearer tokens that expire. SiteSettings
        ' auto-refreshes the token (every TokenUpdateInterval minutes), but UserData.Responser
        ' is a COPY made at download start in UserDataBase.DownloadData() — before DownloadDataF
        ' runs. Even if the token was fresh at copy time it may have since expired. We call
        ' UpdateTokenIfRequired() here to proactively refresh, then re-apply the (possibly
        ' updated) token value to our local Responser copy.
        '
        ' Why this matters for silent failures: if the token is stale every GetResponse call
        ' returns HTTP 401 → Responser returns "" → the gifs loop never runs → the download
        ' appears to complete successfully with nothing downloaded and no error in the log.
        Protected Overrides Sub DownloadDataF(ByVal Token As CancellationToken)
            If Not MySettings.UseCookies.Value Then Responser.Cookies.Clear()
            ' Refresh auth token if expired; bail early with a log if the refresh itself fails.
            If Not MySettings.UpdateTokenIfRequired() Then
                MyMainLOG = $"{ToStringForLog()}: RedGifs token refresh failed — download skipped. Check credentials in RedGifs site settings."
                Exit Sub
            End If
            ' Re-sync the token to our Responser copy. UpdateTokenIfRequired() updates the
            ' site-level Responser.Headers; our copy (made earlier) doesn't get that update
            ' automatically, so we push the current token value into our Responser here.
            Responser.Headers.Add("authorization", MySettings.Token.Value)
            DownloadData(1, Token)
        End Sub
        ' Downloads one page of gifs and recurses for subsequent pages (pagination is RECURSIVE).
        '
        ' Silent failure modes — all produce zero downloads and zero errors without the fixes below:
        '   1. GetResponse returns "" due to HTTP failure (expired/invalid token → 401, user
        '      not found → 404, network error). Now logged via the Else branch.
        '   2. GetResponse throws NullReferenceException from PersonalUtilities._ErrorProcessor
        '      being null (same bug as Reddit). Now caught and treated as case 1.
        ' Note: Exit Sub on duplicate postID is expected behaviour (API is newest-first; once we
        ' hit a known post everything behind it is also known). Not an error — not logged.
        Private Overloads Sub DownloadData(ByVal Page As Integer, ByVal Token As CancellationToken)
            Dim URL$ = String.Empty
            Try
                Dim _page As Func(Of String) = Function() If(Page = 1, String.Empty, $"&page={Page}")
                URL = $"https://api.redgifs.com/v2/users/{Name}/search?order=recent{_page.Invoke}"
                ' SafeGetResponse swallows the PersonalUtilities _ErrorProcessor NullRef (Bug 3);
                ' the empty-response Else branch below logs the HTTP status.
                Dim r$ = SafeGetResponse(Responser, URL)
                Dim postDate$, postID$
                Dim pTotal% = 0
                If Not r.IsEmptyString Then
                    Using j As EContainer = JsonDocument.Parse(r).XmlIfNothing
                        If j.Contains("gifs") Then
                            pTotal = j.Value("pages").FromXML(Of Integer)(0)
                            ProgressPre.ChangeMax(j("gifs").Count)
                            For Each g As EContainer In j("gifs")
                                ProgressPre.Perform()
                                postDate = g.Value("createDate")
                                Select Case CheckDatesLimit(postDate, UnixDate32Provider)
                                    Case DateResult.Skip : Continue For
                                    Case DateResult.Exit : Exit Sub
                                End Select
                                postID = g.Value("id")
                                If Not _TempPostsList.Contains(postID) Then
                                    _TempPostsList.Add(postID)
                                Else
                                    ' This post is already known. Since the API returns newest-first,
                                    ' everything behind it is also already known — stop here.
                                    ' This is normal on re-downloads and is not an error; no log entry.
                                    Exit Sub
                                End If
                                ObtainMedia(g, postID, postDate)
                            Next
                        End If
                    End Using
                Else
                    ' Empty response — log the HTTP status so the cause is visible in the log.
                    ' Common causes: expired/invalid token (401), user not found (404), network error.
                    Dim sc% = CInt(Responser.StatusCode)
                    MyMainLOG = $"{ToStringForLog()}: RedGifs — no response from page {Page} [{URL}]{If(sc <> 0, $" (HTTP {sc})", String.Empty)}. Token may need refreshing."
                End If
                If pTotal > 0 And Page < pTotal Then DownloadData(Page + 1, Token)
            Catch ex As Exception
                ProcessException(ex, Token, $"data downloading error [{URL}]")
            End Try
        End Sub
#End Region
#Region "Media obtain, extract"
        Private Sub ObtainMedia(ByVal j As EContainer, ByVal PostID As String,
                                Optional ByVal PostDateStr As String = Nothing, Optional ByVal PostDateDate As Date? = Nothing,
                                Optional ByVal State As UStates = UStates.Unknown, Optional ByVal Attempts As Integer = 0)
            Dim tMedia As UserMedia = ExtractMedia(j)
            If Not tMedia.Type = UTypes.Undefined Then _
               _TempMediaList.ListAddValue(MediaFromData(tMedia.Type, tMedia.URL, PostID, PostDateStr, PostDateDate, State, Attempts))
        End Sub
        Private Shared Function ExtractMedia(ByVal j As EContainer) As UserMedia
            If Not j Is Nothing Then
                With j("urls")
                    If .ListExists Then
                        Dim u$ = .Value("hd").IfNullOrEmpty(.Value("sd"))
                        If Not u.IsEmptyString Then
                            Dim ut As UTypes = UTypes.Undefined
                            'Type 1: video
                            'Type 2: image
                            Select Case j.Value("type").FromXML(Of Integer)(0)
                                Case 1 : ut = UTypes.Video
                                Case 2 : ut = UTypes.Picture
                            End Select
                            Return New UserMedia(u, ut)
                        End If
                    End If
                End With
            End If
            Return Nothing
        End Function
#End Region
#Region "ReparseMissing"
        Protected Overrides Sub ReparseMissing(ByVal Token As CancellationToken)
            Dim rList As New List(Of Integer)
            Try
                ' "Download missing posts" (DownloadMissingOnly) skips DownloadDataF entirely
                ' (see UserDataBase.DownloadData), so the listing-page request that would normally
                ' detect a 404'd account and set UserExists=False (via DownloadingException) never
                ' runs — UserExists is left at the EnvirReset default of True. Spend one cheap
                ' user-info request up front (replicating DownloadDataF's token setup), with the
                ' same default error handling as the normal listing request, so a 404 here sets
                ' UserExists=False the same way.
                If DownloadMissingOnly Then
                    If Not MySettings.UseCookies.Value Then Responser.Cookies.Clear()
                    If MySettings.UpdateTokenIfRequired() Then
                        Responser.Headers.Add("authorization", MySettings.Token.Value)
                        Try
                            Responser.GetResponse($"https://api.redgifs.com/v2/users/{Name}")
                        Catch ex As Exception
                            ProcessException(ex, Token, $"existence check error [{ToStringForLog()}]")
                        End Try
                    End If
                End If
                If Not UserExists Then
                    ' Account no longer exists — none of its still-Missing posts can ever become
                    ' recoverable. Give up on all of them now instead of re-fetching each one.
                    If ContentMissingExists Then
                        Dim missingCount% = 0
                        For ci% = 0 To _ContentList.Count - 1
                            If _ContentList(ci).State = UStates.Missing Then
                                rList.Add(ci)
                                missingCount += 1
                            End If
                        Next
                        If missingCount > 0 Then
                            MyMainLOG = $"{ToStringForLog()}: ReparseMissing — account no longer exists; " &
                                        $"removing {missingCount} missing item(s) from missing list."
                            _ForceSaveUserData = True
                        End If
                    End If
                    Exit Sub
                End If
                If ContentMissingExists Then
                    Dim url$, r$
                    Dim u As UserMedia
                    Dim j As EContainer
                    ProgressPre.ChangeMax(_ContentList.Count)
                    For i% = 0 To _ContentList.Count - 1
                        ProgressPre.Perform()
                        If _ContentList(i).State = UStates.Missing Then
                            ThrowAny(Token)
                            u = _ContentList(i)
                            If Not u.Post.ID.IsEmptyString Then
                                ' Strip any stale backslash from stored IDs (literal or percent-encoded).
                                url = String.Format(PostDataUrl, Uri.UnescapeDataString(u.Post.ID).Replace("\", String.Empty).ToLower)
                                Try
                                    r = Responser.GetResponse(url)
                                    If Not r.IsEmptyString Then
                                        j = JsonDocument.Parse(r)
                                        If Not j Is Nothing Then
                                            If If(j("gif")?.Count, 0) > 0 Then
                                                ObtainMedia(j("gif"), u.Post.ID,, u.Post.Date, UStates.Missing, u.Attempts)
                                                rList.Add(i)
                                            End If
                                        End If
                                    End If
                                Catch down_ex As Exception
                                    u.Attempts += 1
                                    _ContentList(i) = u
                                End Try
                            Else
                                rList.Add(i)
                            End If
                        End If
                    Next
                End If
            Catch dex As ObjectDisposedException When Disposed
            Catch ex As Exception
                ProcessException(ex, Token, $"missing data downloading error",, False)
            Finally
                If Not Disposed And rList.Count > 0 Then
                    For i% = rList.Count - 1 To 0 Step -1 : _ContentList.RemoveAt(rList(i)) : Next
                End If
            End Try
        End Sub
#End Region
#Region "Downloader"
        Protected Overrides Sub DownloadContent(ByVal Token As CancellationToken)
            DownloadContentDefault(Token)
        End Sub
#End Region
#Region "Get post data statics"
        ''' <summary>
        ''' https://thumbs4.redgifs.com/abcde-large.jpg?expires -> abcde<br/>
        ''' https://thumbs4.redgifs.com/abcde.mp4?expires -> abcde<br/>
        ''' https://www.redgifs.com/watch/abcde?rel=a -> abcde
        ''' </summary>
        Friend Shared Function GetVideoIdFromUrl(ByVal URL As String) As String
            If Not URL.IsEmptyString Then
                Return RegexReplace(URL, If(URL.Contains("/watch/"), WatchIDRegex, ThumbsIDRegex))
            Else
                Return String.Empty
            End If
        End Function
        Friend Shared Function GetDataFromUrlId(ByVal Obj As String, ByVal ObjIsID As Boolean, ByVal Responser As Responser,
                                                ByVal Host As Plugin.Hosts.SettingsHost, ByVal AccountName As String) As UserMedia
            Dim URL$ = String.Empty
            Try
                If Obj.IsEmptyString Then Return Nothing
                If Not ObjIsID Then
                    Obj = GetVideoIdFromUrl(Obj)
                    If Not Obj.IsEmptyString Then Return GetDataFromUrlId(Obj, True, Responser, Host, AccountName)
                Else
                    If Host Is Nothing Then
                        Host = Settings(RedGifsSiteKey, AccountName)
                        If Host Is Nothing Then Host = Settings(RedGifsSiteKey).Default
                    End If
                    If Not Host Is Nothing AndAlso Host.Source.Available(Plugin.ISiteSettings.Download.Main, True) Then
                        If Responser Is Nothing Then Responser = Host.Responser.Copy
                        ' Strip any stale backslash from the ID before constructing the URL.
                        ' The ID may arrive with a literal backslash OR a percent-encoded one (%5c/%5C)
                        ' if the source URL (e.g. from Reddit JSON) carried it. UnescapeDataString
                        ' converts %5c → \ first, then Replace strips the resulting backslash.
                        Obj = Uri.UnescapeDataString(Obj).Replace("\", String.Empty)
                        URL = String.Format(PostDataUrl, Obj.ToLower)
                        Dim r$ = Responser.GetResponse(URL,, EDP.ThrowException)
                        If Not r.IsEmptyString Then
                            Using j As EContainer = JsonDocument.Parse(r)
                                If Not j Is Nothing Then
                                    Dim tm As UserMedia = ExtractMedia(j("gif"))
                                    tm.Post.ID = Obj
                                    tm.File = CStr(RegexReplace(tm.URL, FilesPattern))
                                    If tm.File.IsEmptyString Then
                                        tm.File.Name = Obj
                                        Select Case tm.Type
                                            Case UTypes.Picture : tm.File.Extension = "jpg"
                                            Case UTypes.Video : tm.File.Extension = "mp4"
                                        End Select
                                    End If
                                    Return tm
                                End If
                            End Using
                        End If
                    Else
                        Return New UserMedia With {.State = UStates.Missing}
                    End If
                End If
                Return Nothing
            Catch ex As Exception
                If Not Responser Is Nothing AndAlso (Responser.Client.StatusCode = DataGone Or Responser.Client.StatusCode = HttpStatusCode.NotFound) Then
                    Return New UserMedia With {.State = DataGone}
                Else
                    Dim m As New UserMedia With {.State = UStates.Missing}
                    Dim _errText$ = "API.RedGifs.UserData.GetDataFromUrlId({0})"
                    If Responser.Client.StatusCode = HttpStatusCode.Unauthorized Then
                        _errText = $"RedGifs credentials have expired [{CInt(Responser.Client.StatusCode)}]: {_errText}"
                        MyMainLOG = String.Format(_errText, URL)
                        Return m
                    Else
                        Return ErrorsDescriber.Execute(EDP.SendToLog, ex, String.Format(_errText, URL), m)
                    End If
                End If
            End Try
        End Function
#End Region
#Region "Single data downloader"
        Protected Overrides Sub DownloadSingleObject_GetPosts(ByVal Data As IYouTubeMediaContainer, ByVal Token As CancellationToken)
            Dim m As UserMedia = GetDataFromUrlId(Data.URL, False, Responser, HOST, AccountName)
            If Not m.State = UStates.Missing And Not m.State = DataGone And (m.Type = UTypes.Picture Or m.Type = UTypes.Video) Then
                m.URL_BASE = MySettings.GetUserPostUrl(Me, m)
                _TempMediaList.Add(m)
            End If
        End Sub
#End Region
#Region "Create media"
        Private Function MediaFromData(ByVal t As UTypes, ByVal _URL As String, ByVal PostID As String,
                                       ByVal PostDateStr As String, ByVal PostDateDate As Date?, ByVal State As UStates, Optional ByVal Attempts As Integer = 0) As UserMedia
            _URL = LinkFormatterSecure(RegexReplace(_URL.Replace("\", String.Empty), LinkPattern))
            ' Strip backslashes from PostID for the same reason as _URL — a trailing \ in a stored
            ' Post ID causes a malformed API URL in ReparseMissing (e.g. "id\?" → HTTP 405).
            PostID = PostID.Replace("\", String.Empty)
            Dim m As New UserMedia(_URL, t) With {.Post = New UserPost With {.ID = PostID}}
            If Not m.URL.IsEmptyString Then m.File = CStr(RegexReplace(m.URL, FilesPattern))
            If Not PostDateStr.IsEmptyString Then
                m.Post.Date = AConvert(Of Date)(PostDateStr, UnixDate32Provider, Nothing)
            ElseIf PostDateDate.HasValue Then
                m.Post.Date = PostDateDate
            Else
                m.Post.Date = Nothing
            End If
            m.State = State
            m.Attempts = Attempts
            Return m
        End Function
#End Region
#Region "Exception"
        Protected Overrides Function DownloadingException(ByVal ex As Exception, ByVal Message As String, Optional ByVal FromPE As Boolean = False,
                                                          Optional ByVal EObj As Object = Nothing) As Integer
            Dim s As WebExceptionStatus = Responser.Status
            Dim sc As HttpStatusCode = Responser.StatusCode
            If sc = HttpStatusCode.NotFound Or s = DataGone Or sc = DataGone Then
                UserExists = False
            ElseIf sc = HttpStatusCode.Unauthorized Then
                MyMainLOG = $"RedGifs credentials have expired [{CInt(sc)}]: {ToStringForLog()}"
            Else
                If Not FromPE Then LogError(ex, Message) : HasError = True
                Return 0
            End If
            Return 1
        End Function
#End Region
    End Class
End Namespace