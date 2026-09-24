namespace FlowValidate.Test
{
    public static class RequestCancellationProbe
    {
        public static bool Observed { get; set; }

        public static CancellationToken ObservedToken { get; set; }

        public static void Reset()
        {
            Observed = false;
            ObservedToken = default;
        }
    }
}
