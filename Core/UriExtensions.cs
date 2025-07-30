namespace Re_RunApp.Core;

using System.Web;

public static class UriExtensions
{
    public static string? GetQueryParameter(this Uri uri, string parameterName)
    {
        var query = uri.Query;
        var queryDictionary = HttpUtility.ParseQueryString(query);
        return queryDictionary[parameterName];
    }
}