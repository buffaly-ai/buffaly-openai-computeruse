/**
 * KCS Utility Module: global
 * ------------------------------------------------------------
 * Central location for portal-wide configuration toggles. These values
 * are read by the rest of the KCS utilities to determine batch sizes,
 * polling cadence, and error-reporting behaviour. Keep any additional
 * global flags here so feature code can reference a single source of truth.
 *
 * Usage example – throttle an interval using shared cadence:
 * ```js
 * setInterval(refreshGrid, Globals.GridRefreshInterval);
 * ```
 */
var Globals = {
    GridRowsPerPage: 20,
    GridRefreshInterval: 1500,
    ErrorReportingUrl: '/JsonWs/Buffaly.Common.ErrorReporting.ashx',
	ReportErrors: true,
	ReturnExceptions: false,
    MaximumErrors: 10
}

