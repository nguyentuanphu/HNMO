/** Formats the same authoritative event instant in Sydney and the visitor's zone, including the date. */
export function websiteEventTime(instant: string, language: 'en' | 'vi', visitorZone = Intl.DateTimeFormat().resolvedOptions().timeZone): { sydney: string; visitor: string } | null {
  if (!/(?:Z|[+-]\d{2}:\d{2})$/i.test(instant)) return null;
  const date = new Date(instant);
  if (Number.isNaN(date.valueOf())) return null;
  const locale = language === 'vi' ? 'vi-VN' : 'en-AU';
  const format = (timeZone: string): string => new Intl.DateTimeFormat(locale, { timeZone, dateStyle: 'medium', timeStyle: 'short', hourCycle: 'h23' }).format(date);
  return { sydney: format('Australia/Sydney'), visitor: format(visitorZone) };
}
