using System.Linq;

namespace Host.Participant
{
    /// Produces sequential pseudonyms ("participant_001", "participant_002", ...)
    /// by inspecting existing files in ParticipantStore. Real identity mapping is kept
    /// off-host per dossier/02_physical_setup.md "Data hygiene".
    public static class ParticipantIdGenerator
    {
        const string Prefix = "participant_";

        public static string NextSequential()
        {
            var existing = ParticipantStore.ListParticipantIds();
            int max = 0;
            foreach (var id in existing)
            {
                if (!id.StartsWith(Prefix)) continue;
                if (int.TryParse(id.Substring(Prefix.Length), out var n) && n > max)
                    max = n;
            }
            return $"{Prefix}{max + 1:D3}";
        }
    }
}
