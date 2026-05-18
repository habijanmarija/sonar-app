using System;

namespace Host.Patients
{
    /// Simple cross-tool event bus. Tools subscribe to PatientLoaded / PatientUnloaded
    /// without taking a direct reference on PatientRegistry. Analogous to imhotep's
    /// PatientEventSystem but typed and slimmer.
    public static class PatientEventSystem
    {
        public static event Action<Patient> PatientLoaded;
        public static event Action<Patient> PatientUnloaded;

        internal static void RaiseLoaded(Patient p) => PatientLoaded?.Invoke(p);
        internal static void RaiseUnloaded(Patient p) => PatientUnloaded?.Invoke(p);

        /// Test helper: clear all subscribers. Production code shouldn't call this.
        public static void ClearAllSubscribers()
        {
            PatientLoaded = null;
            PatientUnloaded = null;
        }
    }
}
