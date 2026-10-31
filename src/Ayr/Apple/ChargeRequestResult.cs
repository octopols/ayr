namespace Ayr.Apple
{
    public sealed class ChargeRequestResult
    {
        private ChargeRequestResult(bool sent, string message)
        {
            Sent = sent;
            Message = message;
        }

        public bool Sent { get; }
        public string Message { get; }

        public static ChargeRequestResult Success(string message) => new ChargeRequestResult(true, message);

        public static ChargeRequestResult NotSent(string reason) => new ChargeRequestResult(false, reason);
    }
}
