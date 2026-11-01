using System;
using Ayr.App;

namespace Ayr.Ui
{
    /// <summary>How the Keyboard page names each Fn key action, and how a confirmation describes what the key now does.</summary>
    public static class FnKeyText
    {
        public static string Label(FnKeyAction action)
        {
            switch (action)
            {
                case FnKeyAction.Nothing:
                    return "Nothing";
                case FnKeyAction.OpenAyr:
                    return "Open Ayr";
                case FnKeyAction.NextPerformance:
                    return "Performance";
                case FnKeyAction.NextFanMode:
                    return "Fans";
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown Fn key action.");
            }
        }

        public static string Effect(FnKeyAction action)
        {
            switch (action)
            {
                case FnKeyAction.Nothing:
                    return "does nothing";
                case FnKeyAction.OpenAyr:
                    return "opens Ayr";
                case FnKeyAction.NextPerformance:
                    return "switches to the next performance level";
                case FnKeyAction.NextFanMode:
                    return "switches to the next fan mode";
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown Fn key action.");
            }
        }
    }
}
