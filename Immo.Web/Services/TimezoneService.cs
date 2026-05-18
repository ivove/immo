using System;
using System.Linq;
using System.Threading.Tasks;
using Immo.Data;
using Microsoft.EntityFrameworkCore;

namespace Immo.Web.Services;

public class TimezoneService
{
    private readonly ImmoContext _context;
    private TimeZoneInfo? _cachedTimezone;
    private bool _fetched;

    public TimezoneService(ImmoContext context)
    {
        _context = context;
    }

    public async Task<TimeZoneInfo> GetPreferredTimezoneAsync()
    {
        if (_fetched)
        {
            return _cachedTimezone ?? TimeZoneInfo.Utc;
        }

        try
        {
            var settings = await _context.AppSettings.FirstOrDefaultAsync();
            var tzId = settings?.PreferredTimezone ?? "UTC";
            _cachedTimezone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch
        {
            _cachedTimezone = TimeZoneInfo.Utc;
        }

        _fetched = true;
        return _cachedTimezone ?? TimeZoneInfo.Utc;
    }

    public TimeZoneInfo GetPreferredTimezone()
    {
        if (_fetched)
        {
            return _cachedTimezone ?? TimeZoneInfo.Utc;
        }

        try
        {
            var settings = _context.AppSettings.FirstOrDefault();
            var tzId = settings?.PreferredTimezone ?? "UTC";
            _cachedTimezone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch
        {
            _cachedTimezone = TimeZoneInfo.Utc;
        }

        _fetched = true;
        return _cachedTimezone ?? TimeZoneInfo.Utc;
    }

    public DateTime Format(DateTime utcTime)
    {
        var tz = GetPreferredTimezone();
        
        // SERILOG and SQLite store in UTC, but kind might be Unspecified
        if (utcTime.Kind == DateTimeKind.Unspecified)
        {
            utcTime = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
        }
        else if (utcTime.Kind == DateTimeKind.Local)
        {
            utcTime = utcTime.ToUniversalTime();
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
    }

    public DateTime? Format(DateTime? utcTime)
    {
        if (!utcTime.HasValue) return null;
        return Format(utcTime.Value);
    }

    public string FormatToString(DateTime utcTime, string format = "yyyy-MM-dd HH:mm:ss")
    {
        return Format(utcTime).ToString(format);
    }

    public string FormatToString(DateTime? utcTime, string format = "yyyy-MM-dd HH:mm:ss", string defaultValue = "")
    {
        if (!utcTime.HasValue) return defaultValue;
        return Format(utcTime.Value).ToString(format);
    }
}
