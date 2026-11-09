namespace Valmods.TowerGuard
{
    // Runtime faults are deliberately separate from the user's persisted Enabled setting.
    internal sealed class RuntimeState
    {
        internal bool Ready;
        internal string Error;
        internal bool Running(bool configuredEnabled) => Ready && Error == null && configuredEnabled;
        internal string Status(bool configuredEnabled) => Error != null ? "faulted" : !Ready ? "initializing" :
            configuredEnabled ? "active" : "disabled_configuration";
        internal bool Suspend(string error)
        {
            if (Error != null) return false;
            Error = error;
            return true;
        }
    }
}
