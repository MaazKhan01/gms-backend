namespace DomainPersistence.Enums;

/// <summary>Direction of a flight booking. Stored as its int value on
/// Flights.FlightType, and served to the client by GET /v1/lookups/flight-types.
/// Return means one booking with two legs (outbound + inbound).</summary>
public enum FlightType
{
    Inbound = 1,
    Outbound = 2,
    Return = 3,
}
