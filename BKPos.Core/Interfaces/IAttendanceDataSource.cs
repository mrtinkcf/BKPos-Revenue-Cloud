namespace BKPos.Core.Interfaces;

public interface IAttendanceDataSource
{
    bool IsConnected();

    IReadOnlyList<AttendancePunch> GetPunches(DateTime from, DateTime to);
}

public enum PunchDirection
{
    In = 0,
    Out = 1,
    Unknown = 2
}

public sealed record AttendancePunch(
    string DeviceUserId,
    DateTime PunchTime,
    PunchDirection Direction);

public sealed class NullAttendanceDataSource : IAttendanceDataSource
{
    public bool IsConnected() => false;

    public IReadOnlyList<AttendancePunch> GetPunches(DateTime from, DateTime to)
        => Array.Empty<AttendancePunch>();
}
