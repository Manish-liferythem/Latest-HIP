namespace In.ProjectEKA.HipService.DataFlow
{
    public class DateRange
    {
        public DateRange(string from, string to)
        {
            From = from;
            To = to;
        }

        public string From { get; }
        public string To { get; }
    }
}