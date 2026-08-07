' Copyright (C) 2023  Andy https://github.com/AAndyProgram
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY
Imports PersonalUtilities.Functions.RegularExpressions
Imports PersonalUtilities.Tools.Web.Cookies
Imports PersonalUtilities.Tools.Web.Clients
Namespace API.ThisVid
    Friend Module Declarations
        Friend Const ThisVidSiteKey As String = "AndyProgram_ThisVid"
        Friend ReadOnly RegExNextPage As RParams = RParams.DMS("class=.pagination-next...a class=.selective..href=""([^""]+)""", 1)
        Friend ReadOnly RegExVideoList As RParams = RParams.DMS("\<a href=""([^""]+)"" title=""[^""]+"" class=""tumbpu""", 1, RegexReturn.List, EDP.ReturnValue)
        Friend ReadOnly RegExVideoListSavedPosts As RParams = RParams.DMS("\<a href=""([^""]+)"" title=""[^""]+"">[\r\n\s]*<span class=""thumb ", 1, RegexReturn.List, EDP.ReturnValue)
        Friend ReadOnly RegExAlbumsList As RParams = RParams.DM("(https://thisvid.com/albums/[^/]+/?)"" title=""([^""]*?)"" class=""tumbpu""", 0, RegexReturn.List, EDP.ReturnValue)
        Friend ReadOnly RegExAlbumsListSaved As RParams = RParams.DM("(https://thisvid.com/albums/[^/]+/?)"" title=""([^""]*?)"">[\r\n\s]*\<span class=""thumb""", 0, RegexReturn.List, EDP.ReturnValue)
        Friend ReadOnly RegExAlbumID As RParams = RParams.DMS("albumId:.'(\d+)'", 1)
        Friend ReadOnly RegExAlbumImagesList As RParams = RParams.DMS("""([^""]+?image\d+/?)""", 1, RegexReturn.List, EDP.ReturnValue)
        Friend ReadOnly RegExAlbumImageUrl As RParams = RParams.DMS("\<img src=""(https?://media.thisvid.com/contents/albums/[^""]+?)""", 1, EDP.ReturnValue)

        Friend ReadOnly RegExVideosThumb1 As RParams = RParams.DMS("preview_url:\s*'([^""']+)'", 1, EDP.ReturnValue)
        Friend ReadOnly RegExVideosThumb2 As RParams = RParams.DMS("preview_url1:\s*'([^""']+)'", 1, EDP.ReturnValue)
        Friend ReadOnly RegExVideoTitle As RParams = RParams.DMS("meta property=.og:title..content=""([^""]*)""", 1, EDP.ReturnValue)
        Friend Class CookieKeeper2 : Inherits CookieKeeper
            Public Overrides Function Update(Optional ByVal e As ErrorsDescriber = Nothing) As Boolean
                If Count > 0 Then
                    If Not ConvertToContainer(_Container, CookieList, EDP.ReturnValue) Then CookieList.RemoveAt(CookieList.Count - 1) Else Return True
                End If
                Return MyBase.Update(e)
            End Function
        End Class
        Friend Class Responser2 : Inherits Responser
            Friend Sub New()
                MyBase.New
                ChangeCookieKeeper()
            End Sub
            Friend Sub New(ByVal f As SFile)
                MyBase.New(f)
                ChangeCookieKeeper()
            End Sub
            Private Sub ChangeCookieKeeper()
                _Cookies = New CookieKeeper2
                Reset()
            End Sub
        End Class
    End Module
End Namespace