using System;

namespace Core.ViewModel.Event;

public class EventTypeDto { public Guid Id { get; set; } public string Name { get; set; } }

public class CreateEventTypeRequest { public string Name { get; set; } }
