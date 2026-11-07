' Copyright (C) 2023  Andy https://github.com/AAndyProgram
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY
Imports PersonalUtilities.Tools.Web.Cookies
Imports PersonalUtilities.Tools.Web.Clients
Imports PersonalUtilities.Tools.Web.Clients.EventArguments
Imports PersonalUtilities.Functions.RegularExpressions
Namespace API.Instagram
    Friend Module Declarations
        Friend Const InstagramSite As String = "Instagram"
        Friend Const InstagramSiteKey As String = "AndyProgram_Instagram"
        Friend ReadOnly FilesPattern As RParams = RParams.DMS(".+?([^/\?]+?\.[\w\d]{3,4})(?=(\?|\Z))", 1, EDP.ReturnValue)
        Friend ReadOnly ObtainMedia_SizeFuncPic_RegexP As RParams = RParams.DMS("_p(\d+)x(\d+)", 1, EDP.ReturnValue)
        Friend ReadOnly ObtainMedia_SizeFuncPic_RegexS As RParams = RParams.DMS("_s(\d+)x(\d+)", 1, EDP.ReturnValue)
        Friend Const PageTokenRegexPatternDefault As String = "\[\],{""token"":""(.*?)""},\d+\]"
        Friend ReadOnly Regex_UserToken_dtsg As RParams = RParams.DMS("DTSGInitialData["":,.\[\]]*?{\s*.token.:\s*""([^""]+)", 1, EDP.ReturnValue)
        Friend ReadOnly Regex_UserToken_lsd As RParams = RParams.DMS("LSD["":,.\[\]]*?{\s*.token.:\s*""([^""]+)", 1, EDP.ReturnValue)
        Friend ReadOnly Regex_BlockVersionID As RParams = RParams.DMS("""versioningID"":""([^""]+)""", 1, EDP.ReturnValue)
        Friend ReadOnly Regex_ProfileID As RParams = RParams.DMS("profilePage_(\d+)", 1, EDP.ReturnValue)
#Region "File name from URL"
        Private ReadOnly UrlSegmentBreaks As Char() = {"/"c, "?"c}
        ''' <summary>
        ''' What <c>RegexReplace(URL, FilesPattern)</c> returns, computed in one pass instead: the URL's last
        ''' path segment, when it is a name ending in a 3–4 character extension and is followed by "?" or
        ''' the end of the URL.
        ''' The regex itself backtracks roughly cubically in the URL's length, because PersonalUtilities
        ''' collects every match and the scan then runs on through the whole query string. On .NET Framework
        ''' 4.8 that costs 0.3–4 s per URL at Instagram's current 500–1,200-character URLs. It runs for every
        ''' stored media item in the de-duplication pass, so a large profile spent most of an hour there.
        ''' </summary>
        Friend Function FileNameFromUrl(ByVal URL As String) As String
            If URL.IsEmptyString Then Return String.Empty
            ' The regex's candidate end points, in the order it reaches them: each "?", then the end.
            Dim segEnd% = URL.IndexOf("?"c)
            If segEnd < 0 Then segEnd = URL.Length
            Do
                If segEnd > 0 Then
                    ' A name can't contain "/" or "?", and the pattern's leading ".+?" needs one character.
                    Dim segStart% = Math.Max(URL.LastIndexOfAny(UrlSegmentBreaks, segEnd - 1) + 1, 1)
                    If segStart < segEnd Then
                        Dim seg$ = URL.Substring(segStart, segEnd - segStart)
                        If EndsWithExtension(seg) Then Return seg
                    End If
                End If
                If segEnd >= URL.Length Then Return String.Empty
                segEnd = URL.IndexOf("?"c, segEnd + 1)
                If segEnd < 0 Then segEnd = URL.Length
            Loop
        End Function
        ''' <summary>"x.ext": at least one character, a dot, then 3–4 of the regex's <c>[\w\d]</c>.</summary>
        Private Function EndsWithExtension(ByVal Name As String) As Boolean
            For extLen% = 3 To 4
                Dim dot% = Name.Length - extLen - 1
                If dot >= 1 AndAlso Name(dot) = "."c Then
                    Dim allWord As Boolean = True
                    For i% = dot + 1 To Name.Length - 1
                        If Not IsRegexWordChar(Name(i)) Then allWord = False : Exit For
                    Next
                    If allWord Then Return True
                End If
            Next
            Return False
        End Function
        ''' <summary>.NET's <c>\w</c>: <c>[\p{L}\p{Mn}\p{Nd}\p{Pc}]</c>.</summary>
        Private Function IsRegexWordChar(ByVal c As Char) As Boolean
            Select Case Char.GetUnicodeCategory(c)
                Case Globalization.UnicodeCategory.UppercaseLetter, Globalization.UnicodeCategory.LowercaseLetter,
                     Globalization.UnicodeCategory.TitlecaseLetter, Globalization.UnicodeCategory.ModifierLetter,
                     Globalization.UnicodeCategory.OtherLetter, Globalization.UnicodeCategory.NonSpacingMark,
                     Globalization.UnicodeCategory.DecimalDigitNumber, Globalization.UnicodeCategory.ConnectorPunctuation
                    Return True
                Case Else
                    Return False
            End Select
        End Function
#End Region
        Friend Sub UpdateResponser(ByVal Source As IResponse, ByRef Destination As Responser, ByVal UpdateWwwClaim As Boolean)
            Const r_wwwClaimName$ = "x-ig-set-www-claim"
            Const r_tokenName$ = SiteSettings.Header_CSRF_TOKEN_COOKIE
            If Not Source Is Nothing Then
                Dim isInternal As Boolean = TypeOf Source Is WebDataResponse
                Dim wwwClaimName$, tokenName$
                If isInternal Then
                    wwwClaimName = r_wwwClaimName
                    tokenName = r_tokenName
                Else
                    wwwClaimName = SiteSettings.Header_IG_WWW_CLAIM
                    tokenName = SiteSettings.Header_CSRF_TOKEN
                End If
                Dim wwwClaim$ = String.Empty
                Dim token$ = String.Empty
                With Source
                    If isInternal Then
                        If UpdateWwwClaim And .HeadersExists Then wwwClaim = .Headers.Value(wwwClaimName)
                        If .CookiesExists Then token = If(.Cookies.FirstOrDefault(Function(c) c.Name = tokenName)?.Value, String.Empty)
                    Else
                        If .HeadersExists Then
                            If UpdateWwwClaim Then wwwClaim = .Headers.Value(wwwClaimName)
                            token = .Headers.Value(tokenName)
                        End If
                    End If
                End With

                If UpdateWwwClaim And Not wwwClaim.IsEmptyString Then Destination.Headers.Add(SiteSettings.Header_IG_WWW_CLAIM, wwwClaim)
                If Not token.IsEmptyString Then Destination.Headers.Add(SiteSettings.Header_CSRF_TOKEN, token)
                If Not isInternal Then
                    Destination.Cookies.Update(Source.Cookies, CookieKeeper.UpdateModes.ReplaceByNameAll, False, EDP.SendToLog)
                    Destination.Cookies.Update(EDP.SendToLog)
                    Destination.SaveSettings()
                End If
            End If
        End Sub
    End Module
End Namespace