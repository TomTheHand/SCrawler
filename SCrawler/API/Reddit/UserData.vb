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
Imports PersonalUtilities.Functions.RegularExpressions
Imports PersonalUtilities.Functions.XML
Imports PersonalUtilities.Tools.ImageRenderer
Imports PersonalUtilities.Tools.Web.Clients
Imports PersonalUtilities.Tools.Web.Clients.Base
Imports PersonalUtilities.Tools.Web.Documents.JSON
Imports SCrawler.API.Base
Imports SCrawler.API.Reddit.RedditViewExchange
Imports SCrawler.API.YouTube.Objects
Imports SCrawler.Plugin.Hosts
Imports CPeriod = SCrawler.API.Reddit.IRedditView.Period
Imports CView = SCrawler.API.Reddit.IRedditView.View
Imports UStates = SCrawler.API.Base.UserMedia.States
Imports UTypes = SCrawler.API.Base.UserMedia.Types
Namespace API.Reddit
    Friend Class UserData : Inherits UserDataBase : Implements IChannelLimits, IRedditView
#Region "Declarations"
        Private Const CannelsLabelName As String = "Channels"
        Friend Const CannelsLabelName_ChannelsForm As String = "RChannels"
        Private ReadOnly Property MySiteSettings As SiteSettings
            Get
                Return DirectCast(HOST.Source, SiteSettings)
            End Get
        End Property
        Private ReadOnly Property DateTrueProvider(ByVal IsChannel As Boolean) As IFormatProvider
            Get
                Return UnixDate32Provider
                'Return If(IsChannel, UnixDate32ProviderReddit, UnixDate64Provider)
            End Get
        End Property
        Private ReadOnly Property UseM3U8 As Boolean
            Get
                Return Settings.UseM3U8 And CBool(DirectCast(HOST.Source, SiteSettings).UseM3U8.Value)
            End Get
        End Property
        Friend Property IsChannel As Boolean = False
        Friend Overrides ReadOnly Property SpecialLabels As IEnumerable(Of String)
            Get
                Return {CannelsLabelName, CannelsLabelName_ChannelsForm, UserLabelName}
            End Get
        End Property
        Private _RedGifsAccount As String = String.Empty
        Friend Property RedGifsAccount As String Implements IRedditView.RedGifsAccount
            Get
                If Not _RedGifsAccount.IsEmptyString Then
                    Return _RedGifsAccount
                ElseIf Not ChannelInfo Is Nothing Then
                    Return ChannelInfo.RedGifsAccount
                Else
                    Return String.Empty
                End If
            End Get
            Set(ByVal acc As String)
                _RedGifsAccount = acc
            End Set
        End Property
        Private _RedditAccount As String = String.Empty
        Friend Property RedditAccount As String Implements IRedditView.RedditAccount
            Get
                If IsChannelForm Then
                    Return _RedditAccount
                Else
                    Return MyBase.AccountName
                End If
            End Get
            Set(ByVal acc As String)
                _RedditAccount = acc
            End Set
        End Property
        Friend Overrides Property AccountName As String
            Get
                Return RedditAccount
            End Get
            Set(ByVal acc As String)
                MyBase.AccountName = acc
            End Set
        End Property
#End Region
#Region "Channels Support"
#Region "IChannelLimits Support"
        Friend Property DownloadLimitCount As Integer? Implements IChannelLimits.DownloadLimitCount
        Friend Property DownloadLimitPost As String Implements IChannelLimits.DownloadLimitPost
        Friend Property DownloadLimitDate As Date? Implements IChannelLimits.DownloadLimitDate
        Friend Overloads Sub SetLimit(Optional ByVal MaxPost As String = "", Optional ByVal MaxCount As Integer? = Nothing,
                                      Optional ByVal MinDate As Date? = Nothing) Implements IChannelLimits.SetLimit
            DownloadLimitPost = MaxPost
            DownloadLimitCount = MaxCount
            DownloadLimitDate = MinDate
        End Sub
        Friend Overloads Sub SetLimit(ByVal Source As IChannelLimits) Implements IChannelLimits.SetLimit
            With Source
                DownloadLimitCount = .DownloadLimitCount
                DownloadLimitPost = .DownloadLimitPost
                DownloadLimitDate = .DownloadLimitDate
                AutoGetLimits = .AutoGetLimits
            End With
        End Sub
        Friend Property AutoGetLimits As Boolean = True Implements IChannelLimits.AutoGetLimits
        Private ReadOnly Property IsChannelForm As Boolean
            Get
                Return Not IsSavedPosts AndAlso IsChannel AndAlso Not ChannelInfo Is Nothing
            End Get
        End Property
#End Region
        Friend Property ChannelInfo As Channel
        Private ReadOnly ChannelPostsNames As List(Of String)
        Friend Property SkipExistsUsers As Boolean = False
        Private ReadOnly _ExistsUsersNames As List(Of String)
        Friend Property SaveToCache As Boolean = False
        Friend Function GetNewChannelPosts() As IEnumerable(Of UserPost)
            If _ContentNew.Count > 0 Then Return (From c As UserMedia In _ContentNew
                                                  Where Not c.Post.CachedFile.IsEmptyString And c.State = UStates.Downloaded
                                                  Select c.Post) Else Return Nothing
        End Function
#End Region
#Region "IRedditView Support"
        Friend Property ViewMode As CView Implements IRedditView.ViewMode
        Friend Property ViewPeriod As CPeriod Implements IRedditView.ViewPeriod
        Friend Sub SetView(ByVal Options As IRedditView) Implements IRedditView.SetView
            If Not Options Is Nothing Then
                With Options
                    ViewMode = .ViewMode
                    ViewPeriod = .ViewPeriod
                    DownloadText = .DownloadText
                    DownloadTextPosts = .DownloadTextPosts
                    DownloadTextSpecialFolder = .DownloadTextSpecialFolder
                    RedGifsAccount = .RedGifsAccount
                    RedditAccount = .RedditAccount
                    If TypeOf Options Is RedditViewExchange Then DirectCast(Options, RedditViewExchange).ApplyBase(Me)
                End With
            End If
        End Sub
        Private ReadOnly Property View As String
            Get
                Select Case ViewMode
                    Case CView.Hot : Return "hot"
                    Case CView.Top : Return "top"
                    Case CView.Best : Return "best"
                    Case CView.Rising : Return "rising"
                    Case Else : Return "new"
                End Select
            End Get
        End Property
        Private ReadOnly Property Period As String
            Get
                If ViewMode = CView.Top Then
                    Select Case ViewPeriod
                        Case CPeriod.Hour : Return "hour"
                        Case CPeriod.Day : Return "day"
                        Case CPeriod.Week : Return "week"
                        Case CPeriod.Month : Return "month"
                        Case CPeriod.Year : Return "year"
                        Case Else : Return "all"
                    End Select
                Else
                    Return "all"
                End If
            End Get
        End Property
        Friend Overrides Property DownloadText As Boolean Implements IRedditView.DownloadText
        Friend Overrides Property DownloadTextPosts As Boolean Implements IRedditView.DownloadTextPosts
        Friend Overrides Property DownloadTextSpecialFolder As Boolean Implements IRedditView.DownloadTextSpecialFolder
#End Region
#Region "Initializer"
        Friend Sub New()
            ChannelPostsNames = New List(Of String)
            _ExistsUsersNames = New List(Of String)
            _CrossPosts = New List(Of String)
            UseMD5Comparison = True
            StartMD5Checked = True
            RemoveExistingDuplicates = False
            UseInternalDownloadFileFunction = True
            UseInternalM3U8Function = True
        End Sub
#End Region
#Region "Load and Update user info"
        Private Function UpdateNames() As Boolean
            If NameTrue(True).IsEmptyString Then
                Dim n$() = Name.Split("@")
                If n.ListExists Then
                    If n.Length = 2 Then
                        NameTrue = n(0)
                        IsChannel = True
                    ElseIf IsChannel Then
                        NameTrue = Name
                    Else
                        NameTrue = n(0)
                    End If
                End If
                If Not IsSavedPosts Then
                    Dim l$ = IIf(IsChannel, CannelsLabelName, UserLabelName)
                    Settings.Labels.Add(l)
                    Labels.ListAddValue(l, LNC)
                    Labels.Sort()
                    Return True
                End If
            End If
            Return False
        End Function
        Protected Overrides Sub LoadUserInformation_OptionalFields(ByRef Container As XmlFile, ByVal Loading As Boolean)
            With Container
                If Loading Then
                    ViewMode = .Value(Name_ViewMode).FromXML(Of Integer)(CInt(CView.New))
                    ViewPeriod = .Value(Name_ViewPeriod).FromXML(Of Integer)(CInt(CPeriod.All))
                    IsChannel = .Value(Name_IsChannel).FromXML(Of Boolean)(False)
                    RedGifsAccount = .Value(Name_RedGifsAccount)
                    RedditAccount = .Value(Name_RedditAccount)
                    UpdateNames()
                Else
                    If UpdateNames() Then .Value(Name_LabelsName) = LabelsString
                    .Add(Name_ViewMode, CInt(ViewMode))
                    .Add(Name_ViewPeriod, CInt(ViewPeriod))
                    .Add(Name_IsChannel, IsChannel.BoolToInteger)
                    .Add(Name_TrueName, NameTrue(True))
                    .Add(Name_RedGifsAccount, RedGifsAccount)
                    .Add(Name_RedditAccount, RedditAccount)
                End If
            End With
        End Sub
        Friend Overrides Function ExchangeOptionsGet() As Object
            Return New RedditViewExchange(Me)
        End Function
        Friend Overrides Sub ExchangeOptionsSet(ByVal Obj As Object)
            If Not Obj Is Nothing AndAlso TypeOf Obj Is IRedditView Then SetView(DirectCast(Obj, IRedditView))
        End Sub
#End Region
#Region "Download Overrides"
        Friend Overrides Sub DownloadData(ByVal Token As CancellationToken)
            Err429Count = 0
            _CrossPosts.Clear()
            If CreatedByChannel And Settings.FromChannelDownloadTopUse And Settings.FromChannelDownloadTop > 0 Then _
               DownloadTopCount = Settings.FromChannelDownloadTop.Value
            If IsChannel Or IsSavedPosts Then UseMD5Comparison = False
            If IsSavedPosts Then NameTrue = MySiteSettings.SavedPostsUserName.Value
            UpdateNames()
            If IsChannelForm Then
                UseMD5Comparison = False
                EnvirDownloadSet()
                If Not Responser Is Nothing Then Responser.Dispose()
                Responser = New Responser
                Responser.Copy(MySiteSettings.Responser)
                ChannelPostsNames.ListAddList(ChannelInfo.PostsAll.Select(Function(p) p.ID), LNC)
                If Not ViewMode = CView.New Then ChannelPostsNames.ListAddList(ChannelInfo.PostsNames, LNC)
                If SkipExistsUsers Then _ExistsUsersNames.ListAddList(Settings.UsersList.Select(Function(p) p.Name), LNC)
                DownloadDataF(Token)
                ReparseVideo(Token)
                _ContentNew.ListAddList(_TempMediaList, LAP.ClearBeforeAdd)
                DownloadContent(Token)
            Else
                MyBase.DownloadData(Token)
            End If
        End Sub
        ' Entry point for all Reddit downloads. Routes to one of three paths:
        '   SavedPosts  → DownloadDataChannel
        '   Channel     → DownloadDataChannel
        '   Regular user→ DownloadDataUser
        '
        ' Responser here is already a copy of SiteSettings.Responser (initialised by the base class),
        ' pre-loaded with the OAuth bearer token header. The With block below optionally strips auth
        ' headers depending on per-download-type settings before any requests are made.
        Protected Overrides Sub DownloadDataF(ByVal Token As CancellationToken)
            With MySiteSettings
                If IsSavedPosts Then
                    If Not CBool(.UseTokenForSavedPosts.Value) Then Responser.Headers.Remove(DeclaredNames.Header_Authorization)
                Else
                    If Not CBool(.UseCookiesForTimelines.Value) Then Responser.Cookies.Clear()
                    If Not CBool(.UseTokenForTimelines.Value) Then Responser.Headers.Remove(DeclaredNames.Header_Authorization)
                End If
            End With

            ' Wire up the 429 rate-limit handler. Err429Process returns EDP.ReturnValue on a 429
            ' (so GetResponse returns empty string instead of throwing) and sets Err429TryAgain=True
            ' so the pagination loop in DownloadDataUser retries the same page after a forced delay.
            Responser.ProcessExceptionDecision = AddressOf Err429Process

            _TotalPostsDownloaded = 0
            If IsSavedPosts Then
                Responser.DecodersError = EDP.ReturnValue
                DownloadDataChannel(String.Empty, Token)
            ElseIf IsChannel Then
                If ChannelInfo Is Nothing Then
                    ChannelPostsNames.ListAddList(_TempPostsList, LNC)
                    If ChannelPostsNames.Count > 0 Then
                        DownloadLimitCount = Nothing
                        With _ContentList.Where(Function(c) c.Post.Date.HasValue)
                            If .Count > 0 Then DownloadLimitDate = .Max(Function(p) p.Post.Date.Value).AddMinutes(-10)
                        End With
                    End If
                    If DownloadTopCount.HasValue Then DownloadLimitCount = DownloadTopCount
                Else
                    GetUserInfo()
                End If
                If SaveToCache Then
                    DownloadText = False
                    DownloadTextPosts = False
                    DownloadTextSpecialFolder = False
                    Try
                        ' Try-catch is intentional: Responser.Decoders is a shared list that can be
                        ' enumerated concurrently, so Add() can throw InvalidOperationException.
                        If Not Responser.Decoders.Contains(SymbolsConverter.Converters.HTML) Then _
                           Responser.Decoders.Add(SymbolsConverter.Converters.HTML)
                    Catch
                    End Try
                End If
                DownloadDataChannel(String.Empty, Token)
                If ChannelInfo Is Nothing Then _TempPostsList.ListAddList(_TempMediaList.Select(Function(m) m.Post.ID), LNC)
            Else
                GetUserInfo()
                DownloadDataUser(String.Empty, Token)
            End If
            ProgressPre.Done()
        End Sub
#End Region
#Region "Download Functions (User, Channel)"
        Private Err429Count As Integer = 0
        Private Err429TryAgain As Boolean = False
        Private _TotalPostsDownloaded As Integer = 0
        Private ReadOnly _CrossPosts As List(Of String)
        Private Const SiteGfycatKey As String = "gfycat"
        Private Const SiteRedGifsKey As String = "redgifs"
        Private Const Node_CrosspostRootId As String = "crosspostRootId"
        Private Const Node_CrosspostParentId As String = "crosspostParentId"
        Private Const Node_CrosspostParent As String = "crosspost_parent"
        Private Sub Wait429()
            With MySiteSettings
                If Not Err429TryAgain Then .RequestCount += 1
                Err429TryAgain = False
                If (.RequestCount Mod 100) = 0 Then
                    ' Proactive self-throttle: pause every 100 requests to avoid triggering a
                    ' real 429 (a real 429 also lands here — Err429Process sets RequestCount=100).
                    ' Without the activity-log/label update below, this is a silent 60-second
                    ' freeze — indistinguishable from a hang. Cooldowns go to the activity log
                    ' (live health view), not MyMainLOG (the error log).
                    Dim waitMsg$ = $"rate-limit self-throttle — pausing 60s (request #{.RequestCount})"
                    DownloadObjects.ActivityLog.Add($"[{Site}] {Name}: {waitMsg}")
                    If Not Progress Is Nothing Then Progress.InformationTemporary = $"Reddit: {waitMsg}"
                    Thread.Sleep(60100)
                End If
            End With
        End Sub
        Private Function Err429Process(ByVal Status As IResponserStatus, ByVal NullArg As Object, ByVal CurrErr As ErrorsDescriber) As ErrorsDescriber
            If Not Status Is Nothing AndAlso Status.StatusCode = 429 Then
                If Err429Count = 0 Then
                    Err429Count += 1
                    MySiteSettings.RequestCount = 100
                    Err429TryAgain = True
                    Return EDP.ReturnValue
                End If
            End If
            Return CurrErr
        End Function
        Private Sub Err429Reset()
            Err429Count = 0
            Err429TryAgain = False
        End Sub
        ' Downloads one page of up to 25 posts and recurses for subsequent pages.
        ' Pagination is RECURSIVE, not iterative — see the call at the bottom of the Try block.
        '
        ' POST = the Reddit "after" cursor token for this page (e.g. "t3_abc123").
        '        Pass String.Empty for the first page.
        '
        ' URL is declared at method scope (outside the Try) so the Catch block can include it in
        ' the error log. IMPORTANT: when the recursive call for page N+1 fails, that inner call
        ' catches and logs its own exception with its own URL variable — the exception does NOT
        ' propagate back up to this frame. So if you see an error log pointing at a "page 2" URL,
        ' it came from the inner invocation's catch, not from this one.
        '
        ' Loop control:
        '   _completed = True      → normal exit (no more pages, or unrecoverable error)
        '   Err429TryAgain = True  → retry the same page after a 429 delay (Continue Do)
        '   Empty response + no retry → logs HTTP status via MyMainLOG, then _completed = True
        '
        ' Silent failure modes — all produce zero downloads without the fixes below:
        '   1. GetResponse returns "" due to HTTP failure (expired/invalid token → 401, user
        '      not found → 404, network error). Now logged via the Else branch.
        '   2. GetResponse throws NullReferenceException from PersonalUtilities._ErrorProcessor
        '      being null (Bug 3). Caught and treated as case 1 (r = String.Empty).
        Private Sub DownloadDataUser(ByVal POST As String, ByVal Token As CancellationToken)
            Dim eObj% = 0
            Dim round% = 0
            Dim URL$ = String.Empty  ' declared here so the Catch block can log it
            Dim _completed As Boolean = False
            Do
                round += 1
                Try
                    Dim PostID$ = String.Empty, PostTmp$ = String.Empty
                    Dim PostDate$
                    Dim n As EContainer, nn As EContainer
                    Dim NewPostDetected As Boolean = False
                    Dim ExistsDetected As Boolean = False
                    ' A post is a crosspost if any of its three crosspost ID fields is non-empty.
                    Dim IsCrossPost As Predicate(Of EContainer) = Function(e) Not e.Value(Node_CrosspostRootId).IsEmptyString Or Not e.Value(Node_CrosspostParentId).IsEmptyString Or Not e.Value(Node_CrosspostParent).IsEmptyString
                    ' If ParseUserMediaOnly is set, skip posts not authored by the target user
                    ' (relevant when browsing a subreddit-style feed that mixes multiple authors).
                    Dim CheckNode As Predicate(Of EContainer) = Function(e) Not ParseUserMediaOnly OrElse If(e("author")?.Value, "/").ToLower.Equals(NameTrue.StringToLower)
                    ' Reddit returns two ID formats: "name" = "t3_abc123" (fullname), "id" = "abc123".
                    ' PostTmp holds the fullname (used as the "after" cursor); PostID holds the bare id.
                    ' This lambda returns whichever is populated, preferring the fullname.
                    Dim _PostID As Func(Of String) = Function() PostTmp.IfNullOrEmpty(PostID)

                    'URL = $"https://gateway.reddit.com/desktopapi/v1/user/{NameTrue}/posts?rtj=only&allow_quarantined=true&allow_over18=1&include=identity&after={POST}&dist=25&sort={View}&t={Period}&layout=classic"
                    URL = $"https://oauth.reddit.com/user/{NameTrue}/submitted.json?rtj=only&allow_quarantined=true&allow_over18=1&include=identity&after={POST}&dist=25&sort={View}&t={Period}&layout=classic"
                    ThrowAny(Token)
                    Wait429()
                    ' SafeGetResponse swallows the PersonalUtilities _ErrorProcessor NullRef (Bug 3)
                    ' and returns "" on any HTTP-level error; the Else branch below logs the status.
                    Dim r$ = SafeGetResponse(Responser, URL)
                    If Not r.IsEmptyString Then
                        Err429Reset()
                        Using w As EContainer = JsonDocument.Parse(r).XmlIfNothing
                            If w.Count > 0 Then
                                'n = w.GetNode(JsonNodesJson)
                                n = w.GetNode(ChannelJsonNodes)
                                If n.ListExists Then
                                    ProgressPre.ChangeMax(n.Count)
                                    For Each nn In n
                                        ProgressPre.Perform()
                                        ThrowAny(Token)
                                        With nn("data")
                                            If .ListExists Then
                                                If CheckNode(.Self) Then

                                                    'Obtain post ID
                                                    PostID = String.Empty
                                                    PostTmp = .Value("name") '.Name
                                                    If PostTmp.IsEmptyString Then PostTmp = .Value("id")
                                                    If PostTmp.IsEmptyString Then Continue For
                                                    'Check for CrossPost
                                                    ' Crossposts are tracked in _CrossPosts so that when
                                                    ' the original post appears later (or was already seen),
                                                    ' it isn't double-downloaded. The fullname (PostTmp) and
                                                    ' all three crosspost ID variants are all stored together.
                                                    If IsCrossPost(.Self) Then
                                                        _CrossPosts.ListAddList({ .Value(Node_CrosspostRootId),
                                                                                  .Value(Node_CrosspostParentId),
                                                                                  .Value(Node_CrosspostParent),
                                                                                  PostTmp}, LNC)
                                                        If ParseUserMediaOnly Then Continue For
                                                    Else
                                                        If Not _CrossPosts.Contains(PostTmp) Then PostID = PostTmp : PostTmp = String.Empty
                                                    End If

                                                    'Download decision
                                                    If Not _TempPostsList.Contains(_PostID()) Then
                                                        NewPostDetected = True
                                                        _TempPostsList.Add(_PostID())
                                                    Else
                                                        If Not _CrossPosts.Contains(_PostID()) Then ExistsDetected = True
                                                        Continue For
                                                    End If
                                                    PostDate = If(.Item("created")?.Value, String.Empty)
                                                    Select Case CheckDatesLimit(PostDate, DateTrueProvider(IsChannel))
                                                        Case DateResult.Skip : Continue For
                                                        Case DateResult.Exit : Exit Sub
                                                    End Select

                                                    ParseContainer(.Self, _PostID(), PostDate,,, GetTextDocument(.Self))
                                                End If
                                            End If
                                        End With
                                    Next
                                End If
                            End If
                        End Using
                        ' If this is the first page (POST is empty) and we only saw already-downloaded
                        ' posts, stop — no point paginating further back through known content.
                        If POST.IsEmptyString And ExistsDetected Then Exit Sub
                        ' Recurse for the next page. _PostID() here is the fullname of the last post
                        ' seen on this page, which becomes the "after" cursor for the next request.
                        ' IMPORTANT: exceptions thrown inside the recursive call are caught by THAT
                        ' call's own Catch block, not this one. They will not surface here.
                        If Not _PostID().IsEmptyString And NewPostDetected Then DownloadDataUser(_PostID(), Token)
                    ElseIf Err429TryAgain Then
                        ' Err429Process set this flag and returned EDP.ReturnValue, so GetResponse
                        ' returned an empty string rather than throwing. Retry the same page.
                        Continue Do
                    Else
                        ' Empty response and not a 429 retry — log the HTTP status so the cause
                        ' is visible. Common causes: expired/invalid token (401), network error.
                        Dim sc% = CInt(Responser.StatusCode)
                        MyMainLOG = $"{ToStringForLog()}: Reddit — no response [{URL}]{If(sc <> 0, $" (HTTP {sc})", String.Empty)}"
                    End If
                    ' Reaching here means: either we processed the page (and recursed or not),
                    ' or the response was empty and it wasn't a retriable 429. Either way, done.
                    _completed = True
                Catch ex As Exception
                    ' ProcessException logs the error and returns 0 for most failures → _completed = True.
                    ' Returns HttpStatusCode.InternalServerError (500) for server errors → retry up to
                    ' round 2, then give up. URL here is this call's page URL (see declaration above).
                    If ProcessException(ex, Token, $"data downloading error [{URL}]",, eObj) = HttpStatusCode.InternalServerError Then
                        If round = 2 Then eObj = HttpStatusCode.InternalServerError
                    Else
                        _completed = True
                    End If
                End Try
            Loop While Not _completed
        End Sub
        ' Downloads one page of channel/subreddit posts and recurses for subsequent pages.
        ' Handles both subreddit feeds and saved-posts; IsSavedPosts switches URL and behavior.
        ' Pagination is RECURSIVE (same pattern as DownloadDataUser).
        '
        ' Silent failure modes — all produce zero downloads without the fixes below:
        '   1. GetResponse returns "" due to HTTP failure (expired/invalid token → 401, rate-limit,
        '      network error). Now logged via the Else branch.
        '   2. GetResponse throws NullReferenceException from PersonalUtilities._ErrorProcessor
        '      being null (same Bug 3 as DownloadDataUser). Now caught; r = String.Empty.
        Private Sub DownloadDataChannel(ByVal POST As String, ByVal Token As CancellationToken)
            Const savedPostsSleepTimer% = 2000
            Dim eObj% = 0
            Dim round% = 0
            Dim URL$ = String.Empty
            Dim _completed As Boolean = False
            Do
                round += 1
                Try
                    Dim PostID$ = String.Empty
                    Dim PostDate$, _UserID$
                    Dim n As EContainer, nn As EContainer, s As EContainer
                    Dim NewPostDetected As Boolean = False
                    Dim ExistsDetected As Boolean = False
                    Dim eCount As Predicate(Of EContainer) = Function(e) e.Count > 0
                    Dim lDate As Date?

                    If IsSavedPosts Then
                        URL = $"https://www.reddit.com/user/{NameTrue}/saved.json?after={POST}"
                        If Not POST.IsEmptyString Then Thread.Sleep(savedPostsSleepTimer)
                    Else
                        URL = $"https://reddit.com/r/{NameTrue}/{View}.json?allow_quarantined=true&allow_over18=1&include=identity&after={POST}&dist=25&sort={View}&t={Period}&layout=classic"
                    End If

                    ThrowAny(Token)
                    Wait429()
                    ' SafeGetResponse swallows the PersonalUtilities _ErrorProcessor NullRef (Bug 3);
                    ' empty string is handled by the Else branch below.
                    Dim r$ = SafeGetResponse(Responser, URL)
                    'If IsSavedPosts Then Err429Count = 0
                    If Not r.IsEmptyString Then
                        Err429Reset()
                        Using w As EContainer = JsonDocument.Parse(r).XmlIfNothing
                            If w.Count > 0 Then
                                n = w.GetNode(ChannelJsonNodes)
                                If Not n Is Nothing AndAlso n.Count > 0 Then
                                    ProgressPre.ChangeMax(n.Count)
                                    For Each nn In n
                                        ProgressPre.Perform()
                                        ThrowAny(Token)
                                        s = nn.ItemF({eCount})
                                        If If(s?.Count, 0) > 0 Then
                                            PostID = s.Value("name")
                                            If PostID.IsEmptyString AndAlso s.Contains("id") Then PostID = s("id").Value

                                            If ChannelPostsNames.Contains(PostID) Then
                                                If ViewMode = CView.New Then ExistsDetected = True Else NewPostDetected = True 'bypass
                                                Continue For
                                            End If
                                            If DownloadLimitCount.HasValue AndAlso _TotalPostsDownloaded >= DownloadLimitCount.Value Then Exit Sub
                                            If Not DownloadLimitPost.IsEmptyString AndAlso DownloadLimitPost = PostID Then Exit Sub
                                            If ViewMode = CView.New AndAlso DownloadLimitDate.HasValue AndAlso _TempMediaList.Count > 0 Then
                                                With (From __u In _TempMediaList Where __u.Post.Date.HasValue Select __u.Post.Date.Value)
                                                    If .Count > 0 Then lDate = .Min Else lDate = Nothing
                                                End With
                                                If lDate.HasValue AndAlso lDate.Value <= DownloadLimitDate.Value Then Exit Sub
                                            End If

                                            If IsSavedPosts Then
                                                If Not _TempPostsList.Contains(PostID) Then
                                                    NewPostDetected = True
                                                    _TempPostsList.Add(PostID)
                                                Else
                                                    ExistsDetected = True
                                                    Continue For
                                                End If
                                            Else
                                                NewPostDetected = True
                                            End If

                                            If s.Contains("created") Then PostDate = s("created").Value Else PostDate = String.Empty
                                            _UserID = s.Value("author")

                                            If Not IsSavedPosts AndAlso SkipExistsUsers AndAlso _ExistsUsersNames.Count > 0 AndAlso
                                               Not _UserID.IsEmptyString AndAlso _ExistsUsersNames.Contains(_UserID) Then
                                                If Not IsSavedPosts AndAlso Not ChannelInfo Is Nothing Then _
                                                   ChannelInfo.ChannelExistentUserNames.ListAddValue(_UserID, LNC)
                                                Continue For
                                            End If

                                            ParseContainer(s, PostID, PostDate, _UserID,, If(Not SaveToCache, GetTextDocument(s), String.Empty))
                                        End If
                                    Next
                                End If
                            End If
                        End Using
                        If POST.IsEmptyString And ExistsDetected Then Exit Sub
                        If Not PostID.IsEmptyString And NewPostDetected Then DownloadDataChannel(PostID, Token)
                    ElseIf Err429TryAgain Then
                        Continue Do
                    Else
                        ' Empty response and not a 429 retry — log the HTTP status so the cause
                        ' is visible. Common causes: expired/invalid token (401), user not found (404), network error.
                        Dim sc% = CInt(Responser.StatusCode)
                        MyMainLOG = $"{ToStringForLog()}: Reddit — no response [{URL}]{If(sc <> 0, $" (HTTP {sc})", String.Empty)}"
                    End If
                    _completed = True
                Catch ex As Exception
                    Dim errValue% = ProcessException(ex, Token, $"{IIf(IsSavedPosts, "saved posts", "channel")} data downloading error [{URL}]",, eObj)
                    If errValue = HttpStatusCode.InternalServerError Then
                        If round = 2 Then eObj = HttpStatusCode.InternalServerError
                    ElseIf errValue = 429 And round = 0 Then
                        Thread.Sleep(savedPostsSleepTimer)
                        round += 1
                    Else
                        _completed = True
                    End If
                End Try
            Loop While Not _completed
        End Sub
#End Region
#Region "GetUserInfo"
        Private Sub GetUserInfo(Optional ByVal Round As Integer = 0)
            Try
                If Not IsSavedPosts And ChannelInfo Is Nothing Then
                    Wait429()
                    Dim r$ = Responser.GetResponse($"https://reddit.com/{IIf(IsChannel, "r", "user")}/{NameTrue}/about.json",, EDP.ReturnValue)
                    If Not r.IsEmptyString Then
                        Err429Reset()
                        Using j As EContainer = JsonDocument.Parse(r)
                            If Not j Is Nothing AndAlso j.Contains({"data", "subreddit"}) Then
                                If ID.IsEmptyString Then ID = j.Value({"data"}, "id")
                                With j({"data", "subreddit"})
                                    UserSiteNameUpdate(.Value("title"))
                                    UserDescriptionUpdate(.Value("public_description"))
                                    Dim dir As SFile = MyFile.CutPath
                                    Dim fileCrFunc As Func(Of String, SFile) = Function(img) CreateFileFromUrl(img)
                                    If DownloadIconBanner Then
                                        SimpleDownloadAvatar(.Value("icon_img"), fileCrFunc)
                                        SimpleDownloadAvatar(.Value("banner_img"), fileCrFunc)
                                    End If
                                End With
                            End If
                        End Using
                    ElseIf Err429TryAgain And Round < 2 Then
                        GetUserInfo(Round + 1)
                    End If
                End If
            Catch ex As Exception
            End Try
        End Sub
#End Region
#Region "ParseContainer"
        Private Function ParseContainer(ByVal e As EContainer, ByVal PostID As String, ByVal PostDate As String, Optional ByVal UserID As String = Nothing,
                                        Optional ByVal AllowReparse As Boolean = True, Optional ByVal PostText As String = Nothing) As Boolean
            If Not e Is Nothing Then
                Dim UPicType As Func(Of String, UTypes) = Function(input) IIf(input = "image", UTypes.Picture, UTypes.GIF)
                Dim eCount As Predicate(Of EContainer) = Function(item) item.Count > 0
                Dim added As Boolean = True
                Dim tmpUrl$ = e.Value("url").IfNullOrEmpty(e.Value({"source"}, "url"))
                If Not tmpUrl.IsEmptyString AndAlso tmpUrl.StringContains({$"{SiteRedGifsKey}.com", $"{SiteGfycatKey}.com"}) Then
                    If SaveToCache Then
                        tmpUrl = e.Value({"media", "oembed"}, "thumbnail_url")
                        If Not tmpUrl.IsEmptyString Then
                            _TempMediaList.ListAddValue(MediaFromData(UTypes.Picture, tmpUrl, PostID, PostDate, UserID,, PostText), LNC)
                            _TotalPostsDownloaded += 1
                        Else
                            added = False
                        End If
                    Else
                        _TempMediaList.ListAddValue(MediaFromData(UTypes.VideoPre, tmpUrl, PostID, PostDate, UserID,, PostText), LNC)
                        _TotalPostsDownloaded += 1
                    End If
                ElseIf CreateImgurMedia(tmpUrl, PostID, PostDate, UserID, IsChannel, PostText) Then
                    _TotalPostsDownloaded += 1
                ElseIf DownloadGallery(e, PostID, PostDate, UserID, SaveToCache, PostText) Then
                    _TotalPostsDownloaded += 1
                ElseIf Not If(e({"media"}, "type")?.Value, String.Empty).IsEmptyString Then
                    With e("media")
                        Dim t$ = .Item("type").Value
                        Select Case t
                            Case "gallery" : If DownloadGallery(.Self, PostID, PostDate,,, PostText) Then _TotalPostsDownloaded += 1 Else added = False
                            Case "image", "gifvideo"

                                Dim resolution As Sizes = Nothing
                                Dim content As Sizes = Nothing
                                Dim chosenVal$ = String.Empty
                                ParseResolutions(e("media"), e("preview"), resolution)
                                If .Contains("content") Then
                                    content = CreateSize(.Self, "content")
                                    If content.HasError Or content.Data.IsEmptyString Then content = Nothing
                                End If

                                If UPicType(t) = UTypes.Picture Then
                                    If Not content.Data.IsEmptyString Then
                                        If Not resolution.Data.IsEmptyString Then
                                            If content.Value >= resolution.Value AndAlso TryImage(content.Data) Then
                                                chosenVal = content.Data
                                            Else
                                                chosenVal = resolution.Data
                                            End If
                                        Else
                                            chosenVal = content.Data
                                        End If
                                    Else
                                        chosenVal = resolution.Data
                                    End If
                                Else
                                    If Not resolution.Data.IsEmptyString Then
                                        chosenVal = resolution.Data
                                    ElseIf Not content.Data.IsEmptyString Then
                                        chosenVal = content.Data
                                    End If
                                End If

                                If Not chosenVal.IsEmptyString Then
                                    _TempMediaList.ListAddValue(MediaFromData(UPicType(t), chosenVal, PostID, PostDate, UserID,, PostText), LNC)
                                    _TotalPostsDownloaded += 1
                                Else
                                    added = False
                                End If
                            Case "video"
                                If UseM3U8 AndAlso .Item("hlsUrl").XmlIfNothingValue("/").ToLower.Contains("m3u8") Then
                                    _TempMediaList.ListAddValue(MediaFromData(UTypes.m3u8, .Value("hlsUrl"), PostID, PostDate, UserID,, PostText), LNC)
                                    _TotalPostsDownloaded += 1
                                ElseIf Not UseM3U8 AndAlso .Item("fallback_url").XmlIfNothingValue("/").ToLower.Contains("mp4") Then
                                    _TempMediaList.ListAddValue(MediaFromData(UTypes.Video, .Value("fallback_url"), PostID, PostDate, UserID,, PostText), LNC)
                                    _TotalPostsDownloaded += 1
                                Else
                                    added = False
                                End If
                            Case Else : added = False
                        End Select
                    End With
                ElseIf Not If(e({"media", "reddit_video"}, "fallback_url")?.Value, String.Empty).IsEmptyString Then
                    tmpUrl = e({"media", "reddit_video"}, "fallback_url").Value
                    If SaveToCache Then
                        tmpUrl = GetVideoRedditPreview(e)
                        If Not tmpUrl.IsEmptyString Then
                            _TempMediaList.ListAddValue(MediaFromData(UTypes.Picture, tmpUrl, PostID, PostDate, UserID, False, PostText), LNC)
                            _TotalPostsDownloaded += 1
                        Else
                            added = False
                        End If
                    ElseIf UseM3U8 AndAlso Not If(e({"media", "reddit_video"}, "hls_url")?.Value, String.Empty).IsEmptyString Then
                        _TempMediaList.ListAddValue(MediaFromData(UTypes.m3u8, e.Value({"media", "reddit_video"}, "hls_url"), PostID, PostDate, UserID,, PostText), LNC)
                        _TotalPostsDownloaded += 1
                    Else
                        _TempMediaList.ListAddValue(MediaFromData(UTypes.Video, tmpUrl, PostID, PostDate, UserID,, PostText), LNC)
                        _TotalPostsDownloaded += 1
                    End If
                Else
                    added = False
                End If
                If Not added Then
                    If AllowReparse Then
                        If If(e.ItemF({"crosspost_parent_list", 0})?.Count, 0) > 0 Then
                            added = ParseContainer(e.ItemF({"crosspost_parent_list", 0}), PostID, PostDate, UserID, True, PostText)
                        Else
                            Dim tPostId$ = e.Value(Node_CrosspostParent).IfNullOrEmpty(e.Value(Node_CrosspostParentId)).IfNullOrEmpty(e.Value(Node_CrosspostRootId))
                            If Not PostID.IsEmptyString And Not tPostId.IsEmptyString Then
                                For ri% = 0 To 1
                                    Wait429()
                                    Dim r$ = Responser.GetResponse($"https://www.reddit.com/comments/{tPostId.Split("_").LastOrDefault}/.json",, EDP.ReturnValue)
                                    If Not r.IsEmptyString Then
                                        Err429Reset()
                                        Using j As EContainer = JsonDocument.Parse(r, EDP.ReturnValue)
                                            If j.ListExists Then
                                                With j.ItemF({0, "data", "children", 0, "data"})
                                                    If .ListExists Then added = ParseContainer(.Self, PostID, PostDate, UserID, False, PostText)
                                                End With
                                            End If
                                        End Using
                                        Exit For
                                    End If
                                Next
                            End If
                        End If
                    End If
                    If Not added Then
                        Dim node As EContainer = e({"source", "url"})
                        Dim tmpType As UTypes = UTypes.Undefined
                        If Not If(node?.Value, String.Empty).IsEmptyString Then
                            With node.Value.ToLower
                                Select Case True
                                    Case .Contains(SiteRedGifsKey), .Contains(SiteGfycatKey) : If Not SaveToCache Then tmpType = UTypes.VideoPre
                                    Case .Contains("m3u8") : If Settings.UseM3U8 And Not SaveToCache Then tmpType = UTypes.m3u8
                                    Case .Contains(".gif") And TryFile(node.Value) : tmpType = UTypes.GIF
                                    Case TryFile(node.Value) : tmpType = UTypes.Picture
                                    Case Else : tmpType = UTypes.Undefined
                                End Select
                            End With
                            If Not tmpType = UTypes.Undefined Then
                                _TempMediaList.ListAddValue(MediaFromData(tmpType, node.Value, PostID, PostDate, UserID,, PostText), LNC)
                                added = True
                            End If
                        End If
                        ' Before falling back to the signed CDN preview URL, try the original
                        ' source URL that came from the post's "url" field (stored in tmpUrl).
                        ' For cross-posts and external images this is the upstream source
                        ' (catbox.moe, cyberdrop.me, etc.) which doesn't expire and won't
                        ' generate a deterministic-but-broken CDN token like external-preview.redd.it.
                        ' We skip reddit.com domains (self-posts, cross-post links) and only proceed
                        ' when TryFile confirms the URL carries a recognisable file extension.
                        If Not added And Not tmpUrl.IsEmptyString _
                           And Not tmpUrl.Contains("reddit.com") _
                           And Not tmpUrl.Contains("redd.it") _
                           And TryFile(tmpUrl) Then
                            ' Type the URL by extension instead of assuming Picture: external hosts
                            ' (catbox.moe, cyberdrop.me, etc.) also serve .gif and video files, and
                            ' mis-tagging those as Picture routes them to the wrong folder and skips
                            ' GIF/video handling. Mirrors the source.url typing Select Case above;
                            ' redgifs/gfycat are already handled earlier so they aren't repeated here.
                            Dim fbType As UTypes = UTypes.Picture
                            With tmpUrl.ToLower
                                If .Contains(".gif") Then
                                    fbType = UTypes.GIF
                                ElseIf .Contains(".mp4") Or .Contains(".webm") Or .Contains(".mov") Or .Contains(".m4v") Then
                                    fbType = UTypes.Video
                                End If
                            End With
                            _TempMediaList.ListAddValue(MediaFromData(fbType, tmpUrl, PostID, PostDate, UserID, False, PostText), LNC)
                            _TotalPostsDownloaded += 1
                            added = True
                        End If
                        If Not added And e.Contains("preview") Then
                            With e.ItemF({"preview", "images", eCount})
                                If .ListExists Then
                                    tmpType = UTypes.Undefined
                                    tmpUrl = String.Empty
                                    Dim sv$ = .Value({"source"}, "url")
                                    If Not sv.IsEmptyString AndAlso sv.Contains(".gif") Then
                                        tmpUrl = .Value({"variants", "gif", "source"}, "url")
                                        If Not tmpUrl.IsEmptyString Then tmpType = UTypes.GIF
                                    End If
                                    If tmpUrl.IsEmptyString Then
                                        tmpUrl = .Value({"variants", "mp4", "source"}, "url")
                                        If Not tmpUrl.IsEmptyString Then tmpType = UTypes.Video
                                    End If
                                    If tmpUrl.IsEmptyString Then
                                        tmpUrl = .Value({"source"}, "url")
                                        If Not tmpUrl.IsEmptyString Then tmpType = UTypes.Picture
                                    End If
                                    If Not tmpUrl.IsEmptyString And Not tmpType = UTypes.Undefined Then
                                        Dim m As UserMedia = MediaFromData(tmpType, tmpUrl, PostID, PostDate, UserID,, PostText)
                                        If tmpType = UTypes.Video Then m.File.Extension = "mp4"
                                        _TempMediaList.ListAddValue(m, LNC)
                                        _TotalPostsDownloaded += 1
                                        added = True
                                    End If
                                End If
                            End With
                        End If
                    End If
                End If
                If Not added And Not PostText.IsEmptyString Then _TempMediaList.ListAddValue(MediaFromData(UTypes.Text, String.Empty, PostID, PostDate, UserID,, PostText))
                Return added
            Else
                Return False
            End If
        End Function
        Private Function TryImage(ByVal URL As String) As Boolean
            Try
                If Not CBool(MySiteSettings.CheckImage.Value) Then
                    Return MySiteSettings.CheckImageReturnOrig.Value
                Else
                    Dim img As Image = GetImage(SFile.GetBytesFromNet(URL, EDP.ThrowException), EDP.ThrowException)
                    If Not img Is Nothing Then
                        img.Dispose()
                        Return True
                    Else
                        Return False
                    End If
                End If
            Catch
                Return False
            End Try
        End Function
        Private Function GetTextDocument(ByVal e As EContainer) As String
            Dim t$ = String.Empty
            Try
                t = e.Value("title")
                With e({"rtjson", "document"})
                    If .ListExists Then
                        For Each tt As EContainer In .Self
                            t.StringAppendLine(vbCrLf,, False)
                            t.StringAppendLine(If(tt.ItemF({"c", 0, "t"})?.Value, String.Empty))
                        Next
                    End If
                End With
            Catch
            End Try
            Return t
        End Function
#End Region
#Region "Download Base Functions"
        Private Function CreateImgurMedia(ByVal _URL As String, ByVal PostID As String, ByVal PostDate As String,
                                          Optional ByVal _UserID As String = "", Optional ByVal IsChannel As Boolean = False,
                                          Optional ByVal PostText As String = Nothing) As Boolean
            If Not _URL.IsEmptyString AndAlso _URL.Contains("imgur") Then
                If _URL.StringContains({".jpg", ".png", ".jpeg"}) Then
                    _TempMediaList.ListAddValue(MediaFromData(UTypes.Picture, _URL, PostID, PostDate, _UserID,, PostText), LNC)
                ElseIf _URL.Contains(".gifv") Then
                    If SaveToCache Then
                        _TempMediaList.ListAddValue(MediaFromData(UTypes.Picture, _URL.Replace(".gifv", ".gif"), PostID, PostDate, _UserID,, PostText), LNC)
                    Else
                        _TempMediaList.ListAddValue(MediaFromData(UTypes.Video, _URL.Replace(".gifv", ".mp4"), PostID, PostDate, _UserID,, PostText), LNC)
                    End If
                ElseIf _URL.Contains(".mp4") Then
                    _TempMediaList.ListAddValue(MediaFromData(UTypes.Video, _URL, PostID, PostDate, _UserID,, PostText), LNC)
                ElseIf _URL.Contains(".gif") Then
                    _TempMediaList.ListAddValue(MediaFromData(UTypes.GIF, _URL, PostID, PostDate, _UserID,, PostText), LNC)
                Else
                    Dim obj As IEnumerable(Of UserMedia) = Imgur.Envir.GetVideoInfo(_URL, EDP.ReturnValue)
                    If Not obj.ListExists Then
                        If Not TryFile(_URL) Then _URL &= ".jpg"
                        _TempMediaList.ListAddValue(MediaFromData(UTypes.Picture, _URL, PostID, PostDate, _UserID,, PostText), LNC)
                    Else
                        Dim ut As UTypes
                        Dim m As UserMedia
                        For Each data As UserMedia In obj
                            With data
                                If Not .URL.IsEmptyString Then
                                    If Not .File.IsEmptyString Then
                                        Select Case .File.Extension
                                            Case "jpg", "png", "jpeg" : ut = UTypes.Picture
                                            Case "gifv" : ut = IIf(SaveToCache, UTypes.Picture, UTypes.Video)
                                            Case "mp4" : ut = UTypes.Video
                                            Case "gif" : ut = UTypes.GIF
                                            Case Else : ut = UTypes.Picture : .File.Extension = "jpg"
                                        End Select
                                        m = MediaFromData(ut, _URL, PostID, PostDate, _UserID,, PostText)
                                        m.URL = .URL
                                        m.File = .File.File
                                        _TempMediaList.ListAddValue(m, LNC)
                                    End If
                                End If
                            End With
                        Next
                    End If
                End If
                Return True
            Else
                Return False
            End If
        End Function
        Private Function DownloadGallery(ByVal e As EContainer, ByVal PostID As String, ByVal PostDate As String,
                                         Optional ByVal _UserID As String = Nothing, Optional ByVal FirstOnly As Boolean = False,
                                         Optional ByVal PostText As String = Nothing) As Boolean
            Try
                Dim added As Boolean = False
                Dim node As EContainer = Nothing
                If e.Contains("media_metadata") Then
                    node = e("media_metadata")
                ElseIf e.Contains("mediaMetadata") Then
                    node = e("mediaMetadata")
                End If
                If If(node?.Count, 0) > 0 Then
                    Dim t As EContainer
                    For Each n As EContainer In node
                        t = n.ItemF({"s", "u"})
                        If Not t Is Nothing AndAlso Not t.Value.IsEmptyString Then
                            _TempMediaList.ListAddValue(MediaFromData(UTypes.Picture, t.Value, PostID, PostDate, _UserID,, PostText), LNC)
                            added = True
                            If FirstOnly Then Exit For
                        End If
                    Next
                End If
                Return added
            Catch ex As Exception
                ProcessException(ex, Nothing, "gallery parsing error", False)
                Return False
            End Try
        End Function
        Private Function GetVideoRedditPreview(ByVal Node As EContainer) As String
            Try
                If Not Node Is Nothing Then
                    Dim n As EContainer = Node.ItemF({"preview", "images", 0})
                    Dim DestNode$() = Nothing
                    If If(n?.Count, 0) > 0 Then Return ParseResolutions(n)
                End If
                Return String.Empty
            Catch ex As Exception
                ProcessException(ex, Nothing, "reddit video preview parsing error", False)
                Return String.Empty
            End Try
        End Function
        Private Function ParseResolutions(ByVal Node As EContainer, Optional ByVal PreviewNode As EContainer = Nothing,
                                          Optional ByRef SResult As Sizes = Nothing) As String
            Try
                If If(Node?.Count, 0) > 0 Then
                    Dim DestNode$() = Nothing
                    If If(Node("resolutions")?.Count, 0) > 0 Then
                        DestNode = {"resolutions"}
                    ElseIf If(Node({"variants", "nsfw", "resolutions"})?.Count, 0) > 0 Then
                        DestNode = {"variants", "nsfw", "resolutions"}
                    End If
                    If Not DestNode Is Nothing Then
                        With Node(DestNode)
                            Dim sl As List(Of Sizes) = .Select(Function(e) CreateSize(e)).
                                                        ListWithRemove(Function(ss) ss.HasError Or ss.Data.IsEmptyString)
                            If If(PreviewNode?.Count, 0) > 0 Then
                                Dim sp As Sizes = CreateSize(PreviewNode)
                                If Not sp.HasError And Not sp.Data.IsEmptyString Then
                                    If sl Is Nothing Then sl = New List(Of Sizes)
                                    sl.Add(sp)
                                End If
                            End If
                            If sl.ListExists Then
                                Dim s As Sizes
                                sl.Sort()
                                s = sl.First
                                sl.Clear()
                                SResult = s
                                Return s.Data
                            End If
                        End With
                    End If
                End If
                Return String.Empty
            Catch ex As Exception
                Return String.Empty
            End Try
        End Function
        Private Function CreateSize(ByVal Node As EContainer, Optional ByVal UrlNodeName As String = "url") As Sizes
            Return New Sizes(Node.Value("width"), Node.Value(UrlNodeName))
        End Function
#End Region
#Region "ReparseVideo"
        Protected Overrides Sub ReparseVideo(ByVal Token As CancellationToken)
            Dim RedGifsResponser As Responser = Nothing
            Try
                ThrowAny(Token)
                Const v2 As UTypes = UTypes.VideoPre + UTypes.m3u8
                If _TempMediaList.Count > 0 AndAlso _TempMediaList.Exists(Function(p) p.Type = UTypes.VideoPre Or p.Type = v2) Then
                    Dim r$, v$
                    Dim e As New ErrorsDescriber(EDP.ReturnValue)
                    Dim m As UserMedia, m2 As UserMedia
                    Dim RedGifsHost As SettingsHost = Settings(RedGifs.RedGifsSiteKey, RedGifsAccount)
                    Dim _repeatForRedgifs As Boolean
                    If RedGifsHost Is Nothing Then RedGifsHost = Settings(RedGifs.RedGifsSiteKey).Default
                    RedGifsResponser = RedGifsHost.Responser.Copy
                    ProgressPre.ChangeMax(_TempMediaList.Count)
                    For i% = _TempMediaList.Count - 1 To 0 Step -1
                        ThrowAny(Token)
                        ProgressPre.Perform()
                        If _TempMediaList(i).Type = UTypes.VideoPre Or _TempMediaList(i).Type = v2 Then
                            m = _TempMediaList(i)
                            If _TempMediaList(i).Type = UTypes.VideoPre Then
                                Do
                                    _repeatForRedgifs = False
                                    If m.URL.Contains($"{SiteGfycatKey}.com") Then
                                        r = Gfycat.Envir.GetVideo(m.URL)
                                        If Not r.IsEmptyString AndAlso r.Contains("redgifs.com") Then m.URL = r : _repeatForRedgifs = True
                                    ElseIf m.URL.Contains(SiteRedGifsKey) Then
                                        m2 = RedGifs.UserData.GetDataFromUrlId(m.URL, False, RedGifsResponser, RedGifsHost, RedGifsAccount)
                                        If m2.State = UStates.Missing Then
                                            m.State = UStates.Missing
                                            _ContentList.Add(m)
                                            _TempMediaList.RemoveAt(i)
                                        ElseIf m2.State = RedGifs.UserData.DataGone Then
                                            _TempMediaList.RemoveAt(i)
                                        Else
                                            m2.URL_BASE = m.URL
                                            m2.Post = m.Post
                                            _TempMediaList(i) = m2
                                        End If
                                        Continue For
                                    Else
                                        Wait429()
                                        r = Responser.GetResponse(m.URL,, e)
                                        If r.IsEmptyString And Err429TryAgain Then _repeatForRedgifs = True
                                        If Not r.IsEmptyString Then Err429Reset()
                                    End If
                                Loop While _repeatForRedgifs
                            Else
                                r = m.URL
                            End If
                            _TempMediaList(i) = New UserMedia
                            If Not r.IsEmptyString Then
                                v = RegexReplace(r, VideoRegEx)
                                If Not v.IsEmptyString Then
                                    _TempMediaList(i) = New UserMedia With {.Type = UTypes.Video, .URL = v, .File = v, .Post = m.Post, .PostText = m.PostText, .PostTextFile = m.PostTextFile}
                                Else
                                    _TempMediaList.RemoveAt(i)
                                End If
                            End If
                        End If
                    Next
                End If
            Catch ex As Exception
                ProcessException(ex, Token, "video reparsing error", False)
            Finally
                If Not RedGifsResponser Is Nothing Then RedGifsResponser.Dispose()
                ProgressPre.Done()
            End Try
        End Sub
#End Region
#Region "ReparseMissing"
        ' After this many combined failures (re-fetch + download attempts) we stop retrying a missing
        ' post and remove it from the missing list permanently.  The post ID stays in _TempPostsList
        ' (persisted to MyFilePosts) so it won't be re-discovered as "new" on the next full scan.
        ' Value rationale: 10 consecutive failures over multiple days is a reliable signal that the
        ' content is gone from Reddit's CDN or the original host.  Transient rate-limit outages
        ' typically resolve within 1-2 runs.
        Private Const MaxReparseMissingAttempts As Integer = 10
        ' Re-fetches Reddit post JSON for each Missing item in _ContentList and re-parses media from it.
        '
        ' Strategy:
        '   For each Missing item, fetches https://www.reddit.com/comments/{postId}/.json (public endpoint,
        '   no auth required). Two attempts: first with auth headers (Responser), then without (respNoHeaders),
        '   to work around Reddit occasionally rejecting OAuth requests on public endpoints.
        '   Parsed media is appended to _TempMediaList with State=Missing and the original post metadata.
        '   Successfully re-parsed items are tracked in rList and removed from _ContentList at the end.
        '
        ' Why re-fetching still sometimes fails (deterministic CDN signing):
        '   Reddit's preview CDN (preview.redd.it / external-preview.redd.it) signs URLs with an HMAC
        '   that is computed from the image path alone — NOT from a timestamp or session token.  The same
        '   image path always produces the same s= token, so re-fetching the post JSON gives a "fresh"
        '   URL that is byte-for-byte identical to the stored one.  If the image was removed from Reddit's
        '   CDN or the upstream host went offline, re-fetching achieves nothing.
        '   MediaFromData now stores i.redd.it (unsigned) for preview.redd.it images, and ParseContainer
        '   now prefers the original upstream URL over external-preview.redd.it, which mitigates the
        '   problem for new entries.  Existing stuck entries are handled by the give-up mechanism below.
        '
        ' Attempts budget / give-up mechanism:
        '   m.Attempts accumulates across runs from two sources:
        '     1. DownloadContentDefault increments Attempts and sets State=Missing on every file download
        '        failure, then writes the updated item back to _ContentList (persisted to disk).
        '     2. ReparseMissing (this function) increments Attempts when the post re-fetch returns an
        '        empty response, the JSON cannot be parsed, or ParseContainer finds no media.
        '   Once m.Attempts reaches MaxReparseMissingAttempts the item is added to rList without any
        '   re-fetch attempt, logged as "gave up after N failed attempts", and permanently removed from
        '   _ContentList.  The post ID remains in _TempPostsList (saved to MyFilePosts) so it is not
        '   re-discovered as new content on the next full scan.
        '
        ' li loop (after ParseContainer):
        '   lastCount records _TempMediaList.Count before ParseContainer runs. After it returns, items
        '   at indices [lastCount .. _TempMediaList.Count-1] are the ones just added for THIS post.
        '   We iterate over only those new items to stamp them with the post's date/state/attempts.
        '   (Using outer i as the index was a bug: crashes when i >= _TempMediaList.Count, and
        '    silently stamps the wrong item when it doesn't crash.)
        '
        ' Per-iteration outer try-catch (around the entire loop body):
        '   Catches anything that the inner try-catch misses. Primary known causes: (a) _ContentList(i)
        '   throws ArgumentOutOfRangeException if the list shrinks concurrently (e.g. UI thread calling
        '   RemoveMedia while the download thread is iterating); (b) Responser.GetResponse internals
        '   throw OOR in some scenarios where the responsible frame is JIT-inlined into ReparseMissing.
        '   The outer catch logs iteration index + list count + exception type so the root cause is visible.
        '   Also includes a bounds pre-check (If Disposed OrElse i >= _ContentList.Count) for safety.
        '
        ' removed_by_category shortcut (bypasses the Attempts budget):
        '   When the post re-fetch succeeds and j.GetNode(SingleJsonNodes) reaches a real post `data`
        '   object (i.e. children is non-empty — the post still exists in some form) but
        '   ParseContainer finds no media, that data object's `removed_by_category` field is checked.
        '   If Reddit has set it (e.g. "moderator", "reddit", "author", "deleted",
        '   "automod_filtered"), Reddit itself is confirming the post is gone for good, so the item
        '   is added to rList immediately instead of waiting out MaxReparseMissingAttempts.
        '   IMPORTANT: `author = "[deleted]"` alone is deliberately NOT treated as a give-up signal —
        '   it only means the POSTING ACCOUNT was deleted, not that the post/media is gone. Posts by
        '   deleted/banned accounts often still resolve via comments/{id}.json and remain
        '   recoverable. Only removed_by_category (Reddit's own classification of the POST) short-
        '   circuits the budget. When children is empty (see "Inner try-catch" below) there's no
        '   data object to inspect, so this shortcut doesn't apply and Attempts += 1 as before.
        '
        ' Inner try-catch (around GetNode/ParseContainer):
        '   Reddit returns non-standard JSON for deleted/removed posts — the expected data→children→data
        '   path is absent, so GetNode() throws ArgumentOutOfRangeException. The guard ensures one bad
        '   post doesn't abort processing of all remaining missing posts. The post ID is logged.
        '
        ' Disposal race (per-iteration Disposed guard):
        '   If Dispose() is called on another thread while the loop is running (e.g. user removed or app
        '   closed mid-download), _ContentList is cleared (Count→0) and ProgressPre/Responser are disposed.
        '   The pre-check exits the loop as soon as Disposed=True or Count drops to zero. If an exception
        '   fires before the check runs (e.g. NullRef from the now-disposed ProgressPre), the per-iteration
        '   catch exits silently when Disposed is True — consistent with DownloadContentDefault's
        '   "Catch aex As ArgumentOutOfRangeException When Disposed" pattern.
        '
        ' Finally block guard:
        '   rList indices are validated against _ContentList.Count before RemoveAt, in case the Try block
        '   exits mid-loop (e.g. unhandled exception) and leaves the list in an inconsistent state.
        Protected Overrides Sub ReparseMissing(ByVal Token As CancellationToken)
            Dim rList As New List(Of Integer)
            Dim RedGifsResponser As Responser = Nothing
            Try
                If Not ChannelInfo Is Nothing Or SaveToCache Then Exit Sub
                ' "Download missing posts" (DownloadMissingOnly) skips DownloadDataF entirely
                ' (see UserDataBase.DownloadData), so the listing-page request that would normally
                ' detect a 404'd account and set UserExists=False (via DownloadingException) never
                ' runs — UserExists is left at the EnvirReset default of True. Spend one cheap
                ' request up front, with the same default error handling as the normal listing
                ' request, so a 404 here sets UserExists=False the same way. This keeps UserExists
                ' accurate for the UI (account status colour, NonExistingUsers log) and group
                ' filtering — see the note below for why it does NOT gate the reparse loop itself.
                If DownloadMissingOnly And Not IsSavedPosts Then
                    Try
                        Wait429()
                        Responser.GetResponse($"https://reddit.com/{IIf(IsChannel, "r", "user")}/{NameTrue}/about.json")
                        Err429Reset()
                    Catch ex As Exception
                        ProcessException(ex, Token, $"existence check error [{NameTrue}]")
                    End Try
                End If
                ' NOTE: UserExists (set above for DownloadMissingOnly via the about.json check, or
                ' normally via DownloadDataF) is intentionally NOT used to gate the reparse below.
                ' A Reddit post persists independently of the account that created it — a
                ' banned/deleted account's posts can still resolve via comments/{id}.json and get
                ' recovered. Per-post permanent removal is instead detected below via
                ' removed_by_category / the existing Attempts give-up budget.
                If ContentMissingExists Then
                    Dim RedGifsHost As SettingsHost = Settings(RedGifs.RedGifsSiteKey, RedGifsAccount)
                    If RedGifsHost Is Nothing Then RedGifsHost = Settings(RedGifs.RedGifsSiteKey).Default
                    RedGifsResponser = RedGifsHost.Responser.Copy
                    Dim respNoHeaders As Responser = Responser.Copy
                    Dim m As UserMedia, m2 As UserMedia
                    Dim r$ = String.Empty, url$
                    Dim ri As Byte
                    Dim j As EContainer
                    Dim lastCount%, li%
                    Dim rv As New ErrorsDescriber(EDP.ReturnValue)
                    respNoHeaders.Headers.Clear()
                    respNoHeaders.ProcessExceptionDecision = AddressOf Err429Process
                    ProgressPre.ChangeMax(_ContentList.Count)
                    For i% = 0 To _ContentList.Count - 1
                        ' Per-iteration outer guard. Catches anything that escapes the inner JSON
                        ' try-catch — in particular, ArgumentOutOfRangeException from _ContentList(i)
                        ' itself (concurrent shrinkage) or from Responser.GetResponse internals when
                        ' those frames are JIT-inlined and appear as ReparseMissing in the stack.
                        ' OperationCanceledException is NOT caught here so ThrowAny still propagates.
                        Try
                            ' Exit silently if the UserData was disposed (e.g. user removed or app closing
                            ' while download is running) — per DownloadContentDefault pattern. Also guard
                            ' against concurrent shrinkage of _ContentList.
                            If Disposed OrElse i >= _ContentList.Count Then Exit For
                            m = _ContentList(i)
                            ProgressPre.Perform()
                            If m.State = UStates.Missing AndAlso Not m.Post.ID.IsEmptyString Then
                                ' Give up permanently once the retry budget is exhausted.
                                ' m.Attempts accumulates across runs: ReparseMissing increments it
                                ' below when the post fetch returns nothing or ParseContainer finds
                                ' no media; DownloadContentDefault also increments it on each file
                                ' download failure.  Once the budget is spent the item is removed
                                ' from the missing list — post ID stays in _TempPostsList so it
                                ' won't be re-discovered as "new" content.
                                If m.Attempts >= MaxReparseMissingAttempts Then
                                    MyMainLOG = $"{ToStringForLog()}: ReparseMissing — post [{m.Post.ID}] " &
                                                $"gave up after {m.Attempts} failed attempt(s); removing from missing list."
                                    rList.Add(i)
                                    ' Force _ContentList to be saved even when this run downloaded nothing
                                    ' new for this user.  Without this, the removal is only in-memory: the
                                    ' base-class save gate (DownloadedTotal>0 / mcb≠mca / etc.) won't fire
                                    ' for quiet users, so the item reappears at the same attempt count on
                                    ' the next run and gives up again forever — an infinite no-op loop.
                                    _ForceSaveUserData = True
                                Else
                                    ThrowAny(Token)
                                    url = $"https://www.reddit.com/comments/{m.Post.ID.Split("_").LastOrDefault}/.json"
                                    For ri = 0 To 1
                                        Wait429()
                                        r = Responser.GetResponse(url,, rv)
                                        If r.IsEmptyString Then Wait429() : r = respNoHeaders.GetResponse(url,, rv)
                                        If Not (r.IsEmptyString And Err429TryAgain) Then Exit For
                                    Next
                                    If Not r.IsEmptyString Then
                                        Err429Reset()
                                        j = JsonDocument.Parse(r, rv)
                                        If Not j Is Nothing Then
                                            ' Inner try-catch: Reddit sometimes returns non-standard JSON for
                                            ' deleted/removed posts (missing the data→children→data path).
                                            ' GetNode() throws ArgumentOutOfRangeException when the path doesn't exist.
                                            Try
                                                Dim parsedOk As Boolean = False
                                                Dim removedCategory$ = String.Empty
                                                If j.Count > 0 Then
                                                    lastCount = _TempMediaList.Count
                                                    With j.GetNode(SingleJsonNodes)
                                                        If .ListExists Then
                                                            If ParseContainer(.Self, m.Post.ID, String.Empty,,, GetTextDocument(.Self)) Then
                                                                If lastCount <> _TempMediaList.Count Then
                                                                    ' li indexes the newly-added items; i is the outer _ContentList index.
                                                                    ' Bug was: _TempMediaList(i) used both here — crashes when i >= Count,
                                                                    ' and silently updates the wrong slot when it doesn't crash.
                                                                    For li = IIf(lastCount < 0, 0, lastCount) To _TempMediaList.Count - 1
                                                                        m2 = _TempMediaList(li)
                                                                        m2.Post.Date = m.Post.Date
                                                                        m2.State = UStates.Missing
                                                                        m2.Attempts = m.Attempts
                                                                        _TempMediaList(li) = m2
                                                                    Next
                                                                End If
                                                                rList.Add(i)
                                                                parsedOk = True
                                                            Else
                                                                removedCategory = .Value("removed_by_category")
                                                            End If
                                                        End If
                                                    End With
                                                End If
                                                If Not parsedOk Then
                                                    If Not removedCategory.IsEmptyString Then
                                                        ' Reddit itself classifies this post as removed (removed_by_category
                                                        ' is set, e.g. "moderator"/"reddit"/"author"/"deleted") — unlike
                                                        ' author == "[deleted]" (which only means the ACCOUNT is gone, not
                                                        ' the post), this is Reddit's own confirmation the post is gone for
                                                        ' good. No point waiting out the normal Attempts budget.
                                                        MyMainLOG = $"{ToStringForLog()}: ReparseMissing — post [{m.Post.ID}] " &
                                                                    $"removed by Reddit ({removedCategory}); removing from missing list."
                                                        rList.Add(i)
                                                        _ForceSaveUserData = True
                                                    Else
                                                        ' Post JSON returned but no media could be extracted:
                                                        ' deleted content, unsupported format, or empty children.
                                                        ' Increment the attempt counter so this item eventually gives up.
                                                        m = _ContentList(i)
                                                        m.Attempts += 1
                                                        _ContentList(i) = m
                                                        _ForceSaveUserData = True
                                                    End If
                                                End If
                                            Catch jsonEx As Exception When Not TypeOf jsonEx Is OperationCanceledException
                                                MyMainLOG = $"{ToStringForLog()}: ReparseMissing — unexpected JSON for post [{m.Post.ID}]: {jsonEx.Message}"
                                            End Try
                                            j.Dispose()
                                        Else
                                            ' Malformed JSON (parse returned Nothing).
                                            m = _ContentList(i)
                                            m.Attempts += 1
                                            _ContentList(i) = m
                                            _ForceSaveUserData = True
                                        End If
                                    Else
                                        ' Empty HTTP response — post is 404/deleted or we exhausted the
                                        ' 429 retry window.  Increment the attempt counter.
                                        m = _ContentList(i)
                                        m.Attempts += 1
                                        _ContentList(i) = m
                                        _ForceSaveUserData = True
                                    End If
                                End If
                            End If
                        Catch iterEx As Exception When Not TypeOf iterEx Is OperationCanceledException
                            ' If disposed mid-loop (Dispose() cleared _ContentList and disposed ProgressPre/
                            ' Responser), swallow silently — per DownloadContentDefault pattern.
                            If Disposed Then Exit For
                            MyMainLOG = $"{ToStringForLog()}: ReparseMissing — iteration {i} of {_ContentList.Count}: {iterEx.GetType.Name}: {iterEx.Message}"
                        End Try
                    Next
                End If
            Catch ex As Exception
                ProcessException(ex, Token, "missing data downloading error")
            Finally
                If Not RedGifsResponser Is Nothing Then RedGifsResponser.Dispose()
                If rList.Count > 0 Then
                    ' Guard against stale indices: if the Try block threw before completing,
                    ' _ContentList may have fewer items than expected. Skip any out-of-range index
                    ' rather than crashing the Finally block and losing the "downloading data error" context.
                    For i% = rList.Count - 1 To 0 Step -1
                        If rList(i) < _ContentList.Count Then _ContentList.RemoveAt(rList(i))
                    Next
                    rList.Clear()
                End If
                ProgressPre.Done()
            End Try
        End Sub
#End Region
#Region "DownloadSingleObject"
        Protected Overrides Sub DownloadSingleObject_GetPosts(ByVal Data As IYouTubeMediaContainer, ByVal Token As CancellationToken)
            Dim __id$ = RegexReplace(Data.URL, RParams.DMS("comments/([^/]+)", 1, EDP.ReturnValue))
            If Not __id.IsEmptyString Then
                User.File = Data.File
                User.File.Name = String.Empty
                User.File.Extension = String.Empty
                _ContentList.Add(New UserMedia With {.State = UStates.Missing, .Post = __id})
                ReparseMissing(Token)
                ReparseVideo(Token)
            End If
        End Sub
#End Region
#Region "Structure creator"
        Private Function MediaFromData(ByVal t As UTypes, ByVal _URL As String, ByVal PostID As String, ByVal PostDate As String,
                                       Optional ByVal _UserID As String = "", Optional ByVal ReplacePreview As Boolean = True,
                                       Optional ByVal PostText As String = Nothing) As UserMedia
            If _URL.IsEmptyString And t = UTypes.Picture Then Return Nothing
            _URL = LinkFormatterSecure(RegexReplace(_URL.Replace("\", String.Empty), LinkPattern))
            Dim m As New UserMedia(_URL, t) With {.Post = New UserPost With {.ID = PostID, .UserID = _UserID}}
            If t = UTypes.Picture Or t = UTypes.GIF Then m.File = CreateFileFromUrl(m.URL) Else m.File = Nothing
            ' Replace signed CDN preview URLs with stable alternatives.
            '   preview.redd.it  → i.redd.it (all types including pictures):
            '     preview.redd.it carries an s= HMAC token.  Reddit's signing is
            '     deterministic — same image path always produces the same token —
            '     so re-fetching the post never helps once the CDN entry is gone.
            '     i.redd.it serves the original upload without any signing token
            '     and is permanent for as long as the content exists on Reddit.
            '   Other "preview"-style URLs (non-preview.redd.it domain): keep the
            '     existing behaviour of replacing only for video/non-picture types.
            ' m.File is only populated for Picture/GIF types (see the assignment above). For other
            ' types it's the default empty SFile — SFile is a value type, so this is NOT a null and
            ' m.File.File would simply be "". Guarding on Not m.File.IsEmptyString avoids rewriting
            ' the URL to a useless "https://i.redd.it/" (empty filename). A preview.redd.it URL on a
            ' video type isn't expected in practice (video is v.redd.it), but the guard is correct
            ' regardless and keeps the original URL in that case.
            If ReplacePreview AndAlso Not m.File.IsEmptyString Then
                If m.URL.Contains("preview.redd.it/") Then
                    m.URL = $"https://i.redd.it/{m.File.File}"
                ElseIf m.URL.Contains("preview") And Not t = UTypes.Picture Then
                    m.URL = $"https://i.redd.it/{m.File.File}"
                End If
            End If
            If Not PostDate.IsEmptyString Then m.Post.Date = AConvert(Of Date)(PostDate, DateTrueProvider(IsChannel Or IsSavedPosts), Nothing) Else m.Post.Date = Nothing
            If Not PostText.IsEmptyString Then m.PostText = PostText
            Return m
        End Function
        Private Function TryFile(ByVal URL As String) As Boolean
            Try
                Return Not URL.IsEmptyString AndAlso Not CreateFileFromUrl(URL).IsEmptyString
            Catch ex As Exception
                Return False
            End Try
        End Function
        Protected Overrides Function CreateFileFromUrl(ByVal URL As String) As SFile
            Return New SFile(CStr(RegexReplace(URL, FilesPattern)))
        End Function
#End Region
#Region "DownloadContent"
        Private _RedGifsResponser As Responser = Nothing
        Protected Overrides Sub DownloadContent(ByVal Token As CancellationToken)
            If _ContentNew.Count > 0 Then
                Try
                    If Not _RedGifsResponser Is Nothing Then _RedGifsResponser.Dispose()
                    _RedGifsResponser = If(Settings(RedGifs.RedGifsSiteKey, RedGifsAccount), Settings(RedGifs.RedGifsSiteKey).Default).Responser.Copy
                    DownloadContentDefault(Token)
                Finally
                    If Not _RedGifsResponser Is Nothing Then _RedGifsResponser.Dispose() : _RedGifsResponser = Nothing
                End Try
            End If
        End Sub
        Protected Overrides Function DownloadContentDefault_GetRootDir() As String
            If Not IsSavedPosts AndAlso (IsChannel And SaveToCache And Not ChannelInfo Is Nothing) Then
                Return ChannelInfo.CachePath.PathNoSeparator
            Else
                Return MyBase.DownloadContentDefault_GetRootDir()
            End If
        End Function
        Protected Overrides Sub DownloadContentDefault_PostProcessing(ByRef m As UserMedia, ByVal File As SFile, ByVal Token As CancellationToken)
            m.Post.CachedFile = File
            MyBase.DownloadContentDefault_PostProcessing(m, File, Token)
        End Sub
        Protected Overrides Function DownloadContentDefault_ProcessDownloadException() As Boolean
            Return Not IsChannel Or Not SaveToCache
        End Function
        Protected Overrides Function DownloadFile(ByVal URL As String, ByVal Media As UserMedia, ByVal DestinationFile As SFile, ByVal Token As CancellationToken) As SFile
            If _RedGifsResponser.DownloadFile(URL, DestinationFile, EDP.ThrowException) Then
                Return DestinationFile
            Else
                Return Nothing
            End If
        End Function
        Protected Overrides Function ValidateDownloadFile(ByVal URL As String, ByVal Media As UserMedia, ByRef Interrupt As Boolean) As Boolean
            Return URL.Contains(SiteRedGifsKey)
        End Function
        Protected Overrides Function DownloadM3U8(ByVal URL As String, ByVal Media As UserMedia, ByVal DestinationFile As SFile, ByVal Token As CancellationToken) As SFile
            Return M3U8.Download(URL, Media, DestinationFile, Token, Progress, Not IsSingleObjectDownload)
        End Function
        Protected Overrides Function ChangeFileNameByProvider(ByVal f As SFile, ByVal m As UserMedia) As SFile
            If Not IsChannel Or Not SaveToCache Then
                Return MyBase.ChangeFileNameByProvider(f, m)
            Else
                Return f
            End If
        End Function
#End Region
#Region "Exception"
        Protected Overrides Function DownloadingException(ByVal ex As Exception, ByVal Message As String, Optional ByVal FromPE As Boolean = False,
                                                          Optional ByVal EObj As Object = Nothing) As Integer
            With Responser
                If .StatusCode = HttpStatusCode.NotFound Then '404
                    UserExists = False
                ElseIf .StatusCode = HttpStatusCode.Forbidden Then '403
                    UserSuspended = True
                ElseIf .StatusCode = HttpStatusCode.BadGateway Or .StatusCode = HttpStatusCode.ServiceUnavailable Then '502, 503
                    LogError(Nothing, $"[{CInt(Responser.StatusCode)}] Reddit is currently unavailable")
                    Throw New Plugin.ExitException With {.Silent = True}
                ElseIf .StatusCode = HttpStatusCode.GatewayTimeout Then '504
                    Return 1
                ElseIf .StatusCode = HttpStatusCode.Unauthorized Then '401
                    LogError(Nothing, $"[{CInt(Responser.StatusCode)}] Reddit credentials expired")
                    MySiteSettings.SessionInterrupted = True
                    Throw New Plugin.ExitException With {.Silent = True}
                ElseIf .StatusCode = HttpStatusCode.InternalServerError Then '500
                    If Not IsNothing(EObj) AndAlso IsNumeric(EObj) AndAlso CInt(EObj) = HttpStatusCode.InternalServerError Then Return 1
                    Return HttpStatusCode.InternalServerError
                    'ElseIf .StatusCode = 429 And IsSavedPosts And Err429Count = 0 Then '429 (saved)
                    '    Err429Count += 1
                    '    Return 429
                ElseIf .StatusCode = 429 Then '429 (all)
                    'If ((Not IsSavedPosts And CBool(MySiteSettings.UseTokenForTimelines.Value)) Or (IsSavedPosts And CBool(MySiteSettings.UseTokenForSavedPosts.Value))) AndAlso
                    '   Not MySiteSettings.CredentialsExists Then
                    '    LogError(Nothing, "[429] You should use OAuth authorization or disable " &
                    '                      IIf(IsSavedPosts, "token usage for downloading saved posts", "the use of token and cookies for downloading timelines"))
                    'Else
                    '    LogError(Nothing, "Too many requests (429). Try again later!")
                    'End If
                    'MySiteSettings.SessionInterrupted = True
                    'Throw New Plugin.ExitException With {.Silent = True}
                    If ((Not IsSavedPosts And CBool(MySiteSettings.UseTokenForTimelines.Value)) Or (IsSavedPosts And CBool(MySiteSettings.UseTokenForSavedPosts.Value))) AndAlso
                       Not MySiteSettings.CredentialsExists Then
                        LogError(Nothing, "[429] You should use OAuth authorization or disable " &
                                          IIf(IsSavedPosts, "token usage for downloading saved posts", "the use of token and cookies for downloading timelines"))
                    Else
                        LogError(Nothing, "Too many requests (429). Try again later!")
                    End If
                    MySiteSettings.SessionInterrupted = True
                    Throw New Plugin.ExitException With {.Silent = True}
                Else
                    If Not FromPE Then LogError(ex, Message) : HasError = True
                    Return 0
                End If
            End With
            Return 1
        End Function
#End Region
#Region "IDisposable Support"
        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            If Not disposedValue And disposing Then ChannelPostsNames.Clear() : _ExistsUsersNames.Clear() : _CrossPosts.Clear()
            MyBase.Dispose(disposing)
        End Sub
#End Region
    End Class
End Namespace