using System.Text.Json;

namespace Mcctl;

public static class ExceptionExtensions
{
    extension(Exception ex)
    {
        /// <summary>
        /// True for failures caused by the device or the network (error response, unreachable, timeout,
        /// unexpected payload) as opposed to programming errors. Use it in <c>catch (…) when</c> filters.
        /// </summary>
        public bool IsDeviceError => ex is YxcException or HttpRequestException or TaskCanceledException or JsonException;

        /// <summary>True when the device could not be reached at all.</summary>
        public bool IsConnectionError => ex is HttpRequestException or TaskCanceledException;
    }
}
