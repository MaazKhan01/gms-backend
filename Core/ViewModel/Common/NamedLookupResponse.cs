using System;

namespace Core.ViewModel.Common
{
    // Minimal { id, name } shape for "name-only" dropdown lookups (flight
    // types/classes, room types) — see TravelLookupsController.
    public class NamedLookupResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}
