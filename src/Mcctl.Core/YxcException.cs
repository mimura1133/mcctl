namespace Mcctl;

/// <summary>
/// Thrown when a device returns a non-zero <c>response_code</c>.
/// </summary>
public sealed class YxcException : Exception
{
    public YxcException(int responseCode, string path)
        : base($"YXC request '{path}' failed: {Describe(responseCode)} (response_code={responseCode})")
    {
        ResponseCode = responseCode;
        Path = path;
    }

    public int ResponseCode { get; }

    public string Path { get; }

    /// <summary>Human readable text for a YXC response code.</summary>
    public static string Describe(int code) => code switch
    {
        0 => "Successful request",
        1 => "Initializing",
        2 => "Internal error",
        3 => "Invalid request (method not supported)",
        4 => "Invalid parameter",
        5 => "Guarded (not available in current state)",
        6 => "Time out",
        99 => "Firmware updating",
        100 => "Access error",
        101 => "Other errors",
        102 => "Wrong user name",
        103 => "Wrong password",
        104 => "Account expired",
        105 => "Account disconnected / gone off / shut down",
        106 => "Account number reached the limit",
        107 => "Server maintenance",
        108 => "Invalid account",
        109 => "License error",
        110 => "Read only mode",
        111 => "Max stations",
        112 => "Access denied",
        113 => "Need to specify playlist destination",
        114 => "Need to create a new playlist",
        115 => "Simultaneous logins reached the limit",
        200 => "Linking in progress",
        201 => "Unlinking in progress",
        _ => "Unknown error",
    };
}
