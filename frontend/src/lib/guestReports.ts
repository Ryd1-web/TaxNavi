/**
 * Reports bought without an account. The access token is the only proof of ownership,
 * so it is kept on this device (never sent anywhere except TaxNavi's own API).
 */
export interface GuestReport {
  reportId: string;
  accessToken: string;
  email: string;
  savedAt: string;
}

const KEY = 'taxnavi.guestReports';

export function listGuestReports(): GuestReport[] {
  if (typeof window === 'undefined') return [];
  try {
    const parsed = JSON.parse(window.localStorage.getItem(KEY) || '[]');
    return Array.isArray(parsed) ? parsed.filter((r) => r?.reportId && r?.accessToken) : [];
  } catch {
    return [];
  }
}

export function getGuestReport(reportId: string) {
  return listGuestReports().find((r) => r.reportId === reportId) ?? null;
}

export function saveGuestReport(report: Omit<GuestReport, 'savedAt'>) {
  const others = listGuestReports().filter((r) => r.reportId !== report.reportId);
  window.localStorage.setItem(KEY, JSON.stringify([{ ...report, savedAt: new Date().toISOString() }, ...others].slice(0, 20)));
}

export function removeGuestReport(reportId: string) {
  window.localStorage.setItem(KEY, JSON.stringify(listGuestReports().filter((r) => r.reportId !== reportId)));
}
