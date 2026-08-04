using Microsoft.Win32;
using ParquetViewer.Analytics;
using ParquetViewer.Controls;
using ParquetViewer.Helpers;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;

namespace ParquetViewer
{
    public static class AppSettings
    {
        private const string RegistrySubKey = "ParquetViewer";
        private const string AlwaysSelectAllFieldsKey = "AlwaysSelectAllFields";
        private const string DateTimeDisplayFormatKey = "DateTimeDisplayFormat";
        private const string ConsentLastAskedOnVersionKey = "ConsentLastAskedOnVersion";
        private const string AnalyticsDeviceIdKey = "AnalyticsDeviceId";
        private const string AnalyticsDataGatheringConsentKey = "AnalyticsDataGatheringConsent";
        private const string AlwaysLoadAllRecordsKey = "AlwaysLoadAllRecords";
        private const string OpenedFileCountKey = "OpenedFileCount";
        private const string CustomDateFormatKey = "CustomDateFormat";
        private const string DarkModeKey = "DarkMode";
        private const string UserSelectedCultureKey = "UserSelectedCulture";
        private const string QueryEditorZoomLevelKey = "QueryEditorZoomLevel";
        private const string CellPreviewVisibleKey = "CellPreviewVisible";
        private const string CellPreviewHeightKey = "CellPreviewHeight";
        private const string CellPreviewWordWrapKey = "CellPreviewWordWrap";
        private const string CellPreviewPrettyPrintKey = "CellPreviewPrettyPrint";
        private const string LineBreakMarkerEnabledKey = "LineBreakMarkerEnabled";
        private const string LineBreakMarkerGlyphKey = "LineBreakMarkerGlyph";
        private const string LineBreakMarkerColorKey = "LineBreakMarkerColor";

        public static DateFormat DateTimeDisplayFormat
        {
            get => ReadRegistryValue(DateTimeDisplayFormatKey, out int value) ? value.ToEnum(DateFormat.Default) : DateFormat.Default;
            set => SetRegistryValue(DateTimeDisplayFormatKey, (int)value);
        }

        public static bool AlwaysSelectAllFields
        {
            get => ReadRegistryValue(AlwaysSelectAllFieldsKey, out string? temp) && bool.TryParse(temp, out var value) ? value : false;
            set => SetRegistryValue(AlwaysSelectAllFieldsKey, value.ToString());
        }

        public static bool CellPreviewVisible
        {
            //Defaults to on: the grid truncates long values and has tooltips disabled, so without this
            //panel there's no way to read them. Same default-true pattern as CellPreviewWordWrap below.
            get => !ReadRegistryValue(CellPreviewVisibleKey, out string? temp) || !bool.TryParse(temp, out var value) || value;
            set => SetRegistryValue(CellPreviewVisibleKey, value.ToString());
        }

        private const int DefaultCellPreviewHeight = 140;
        private const int MinimumCellPreviewHeight = 60;
        public static int CellPreviewHeight
        {
            //Clamp on read: a stale or hand edited registry value shouldn't be able to collapse the panel
            //to nothing or push the grid off screen.
            get => ReadRegistryValue(CellPreviewHeightKey, out int value) && value >= MinimumCellPreviewHeight
                ? value : DefaultCellPreviewHeight;
            set => SetRegistryValue(CellPreviewHeightKey, Math.Max(value, MinimumCellPreviewHeight));
        }

        public static bool CellPreviewWordWrap
        {
            get => !ReadRegistryValue(CellPreviewWordWrapKey, out string? temp) || !bool.TryParse(temp, out var value) || value;
            set => SetRegistryValue(CellPreviewWordWrapKey, value.ToString());
        }

        public static bool CellPreviewPrettyPrint
        {
            get => ReadRegistryValue(CellPreviewPrettyPrintKey, out string? temp) && bool.TryParse(temp, out var value) ? value : false;
            set => SetRegistryValue(CellPreviewPrettyPrintKey, value.ToString());
        }

        public const string DefaultLineBreakMarkerGlyph = "¶";

        public static bool LineBreakMarkerEnabled
        {
            //Defaults to on: without a marker, a value containing line breaks renders as one run-on line
            get => !ReadRegistryValue(LineBreakMarkerEnabledKey, out string? temp) || !bool.TryParse(temp, out var value) || value;
            set => SetRegistryValue(LineBreakMarkerEnabledKey, value.ToString());
        }

        public static string LineBreakMarkerGlyph
        {
            get => ReadRegistryValue(LineBreakMarkerGlyphKey, out string? value) && !string.IsNullOrEmpty(value)
                ? value : DefaultLineBreakMarkerGlyph;
            set => SetRegistryValue(LineBreakMarkerGlyphKey, string.IsNullOrEmpty(value) ? DefaultLineBreakMarkerGlyph : value);
        }

        /// <summary>
        /// Colour of the line break marker, or null to follow the current theme's hyperlink accent.
        /// </summary>
        public static Color? LineBreakMarkerColor
        {
            get
            {
                if (!ReadRegistryValue(LineBreakMarkerColorKey, out string? value) || string.IsNullOrEmpty(value))
                    return null;

                //Stored as an ARGB integer so it survives a round trip exactly
                return int.TryParse(value, out var argb) ? Color.FromArgb(argb) : null;
            }
            set => SetRegistryValue(LineBreakMarkerColorKey, value?.ToArgb().ToString() ?? string.Empty);
        }

        /// <summary>
        /// Resolves the marker colour for a theme, falling back to that theme's hyperlink accent.
        /// </summary>
        public static Color GetLineBreakMarkerColor(Theme theme) => LineBreakMarkerColor ?? theme.HyperlinkColor;

        public static bool AlwaysLoadAllRecords
        {
            get => ReadRegistryValue(AlwaysLoadAllRecordsKey, out string? temp) && bool.TryParse(temp, out var value) ? value : false;
            set => SetRegistryValue(AlwaysLoadAllRecordsKey, value.ToString());
        }

        public static SemanticVersion? ConsentLastAskedOnVersion
        {
            get => ReadRegistryValue(ConsentLastAskedOnVersionKey, out string? value) ? SemanticVersion.TryParse(value, out var semanticVersion) ? semanticVersion : null : null;
            set => SetRegistryValue(ConsentLastAskedOnVersionKey, value?.ToString() ?? string.Empty);
        }

        public static Guid AnalyticsDeviceId
            => ReadRegistryValue(AnalyticsDeviceIdKey, out string? temp) && Guid.TryParse(temp, out var value) ? value : SetAnalyticsDeviceId();

        private static Guid SetAnalyticsDeviceId()
        {
            try
            {
                Guid newDeviceId = Guid.NewGuid();
                SetRegistryValue(AnalyticsDeviceIdKey, newDeviceId);
                return newDeviceId;
            }
            catch
            {
                return Guid.Empty;
            }
        }

        public static bool AnalyticsDataGatheringConsent
        {
            get => ReadRegistryValue(AnalyticsDataGatheringConsentKey, out string? temp) && bool.TryParse(temp, out var value) ? value : false;
            set => SetRegistryValue(AnalyticsDataGatheringConsentKey, value.ToString());
        }

        private static int? _openedFileCount;
        public static int OpenedFileCount
        {
            get => _openedFileCount ??= ReadRegistryValue(OpenedFileCountKey, out int value) ? value : 0;
            set
            {
                _openedFileCount = value;
                SetRegistryValue(OpenedFileCountKey, value);
            }
        }

        private static string? _customDateFormat;
        public static string? CustomDateFormat
        {
            get => _customDateFormat ??= ReadRegistryValue(CustomDateFormatKey, out string? value) && UtilityMethods.IsValidDateFormat(value) ? value : null;
            set
            {
                _customDateFormat = value;
                SetRegistryValue(CustomDateFormatKey, value ?? string.Empty);
            }
        }

        public static bool DarkMode
        {
            get => ReadRegistryValue(DarkModeKey, out string? temp) && bool.TryParse(temp, out var value) ? value : false;
            set
            {
                SetRegistryValue(DarkModeKey, value.ToString());
                var theme = GetTheme();
                foreach (var form in FormBase.OpenForms)
                {
                    form.SetTheme(theme);
                }
            }
        }

        public static Theme GetTheme() => DarkMode ? Theme.DarkModeTheme : Theme.LightModeTheme;

        public static CultureInfo? UserSelectedCulture
        {
            get => ReadRegistryValue(UserSelectedCultureKey, out string? value) ?
                (UtilityMethods.TryParseCultureInfo(value, out CultureInfo? cultureInfo) ? cultureInfo : null)
                : null;
            set => SetRegistryValue(UserSelectedCultureKey, value?.ToString() ?? string.Empty);
        }

        public static int? QueryEditorZoomLevel
        {
            get => ReadRegistryValue(QueryEditorZoomLevelKey, out int value) ? value : null;
            set => SetRegistryValue(QueryEditorZoomLevelKey, value);
        }

        private static bool ReadRegistryValue<T>(string key, [NotNullWhen(true)] out T? value)
        {
            try
            {
                using var registryKey = Registry.CurrentUser.CreateSubKey(RegistrySubKey);
                if (registryKey.GetValue(key) is T castValue)
                {
                    value = castValue;
                    return true;
                }
                else
                {
                    value = default;
                    return false;
                }
            }
            catch (Exception ex)
            {
                //Falling back to the default is intentional, but log why: a setting silently reverting to
                //its default is otherwise indistinguishable from the user never having set it.
                Trace.TraceError($"Failed to read setting `{key}` from the registry: {ex}");

                value = default;
                return false;
            }
        }

        private static void SetRegistryValue<T>(string key, T value)
        {
            if (value is null) //registry can't store null values
                throw new ArgumentNullException(nameof(value));

            try
            {
                using var registryKey = Registry.CurrentUser.CreateSubKey(RegistrySubKey);
                registryKey.SetValue(key, value);
            }
            catch (Exception ex)
            {
                //Failing to persist a setting shouldn't be fatal, but it also shouldn't vanish silently:
                //it explains why a setting the user changed doesn't survive a restart.
                Trace.TraceError($"Failed to save setting `{key}` to the registry: {ex}");
                ExceptionEvent.FireAndForget(ex);
            }
        }
    }
}