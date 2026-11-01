using System;

namespace Ayr.App
{
    public static class ExceptionText
    {
        /// <summary>The message, or the error's type and code when Windows gave no message (common for COM errors).</summary>
        public static string Of(Exception error) =>
            string.IsNullOrWhiteSpace(error.Message)
                ? error.GetType().Name + " 0x" + error.HResult.ToString("X8")
                : error.Message;
    }
}
