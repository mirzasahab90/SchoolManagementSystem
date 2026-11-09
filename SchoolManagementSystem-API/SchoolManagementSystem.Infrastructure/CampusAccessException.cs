namespace SchoolManagementSystem.Infrastructure;

/// <summary>Thrown when a save would create or change data of a campus the user cannot access. Returned as 403.</summary>
public class CampusAccessException(string message) : Exception(message);
