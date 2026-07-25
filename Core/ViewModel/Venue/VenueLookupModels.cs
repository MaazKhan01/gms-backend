namespace Core.ViewModel.Venue;

// Venue reference data — dedicated tables replacing the old generic lookups.
public class VenueTypeDto { public Guid Id { get; set; } public string Name { get; set; } public string NameAr { get; set; } }
public class ElementTypeDto { public Guid Id { get; set; } public string Code { get; set; } public string Name { get; set; } public string NameAr { get; set; } }

public class CreateVenueTypeRequest { public string Name { get; set; } public string NameAr { get; set; } }
public class CreateElementTypeRequest { public string Code { get; set; } public string Name { get; set; } public string NameAr { get; set; } }
