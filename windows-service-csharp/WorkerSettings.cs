namespace APICallerService
{ //Holds configs like delay and watchpath, for the json
// The json itself is settings of things like delay of updates, what file, etc.
    public class WorkerSettings
    {
        public int DelayMilliseconds { get; set; }
        public string? WatchPath { get; set; }
    }
}
