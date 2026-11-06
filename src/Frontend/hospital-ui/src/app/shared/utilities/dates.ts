/**
 * Calendar dates (API `DateOnly`, "yyyy-MM-dd") must not go through toISOString(), which converts to UTC and can
 * shift the day in Asia/Dhaka (UTC+6). These helpers keep the local calendar day.
 */
export function toIsoDate(date: Date | null | undefined): string | null {
  if (!date) {
    return null;
  }
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function fromIsoDate(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

/** The hospital's calendar day (Asia/Dhaka), independent of the workstation's time zone, as "yyyy-MM-dd". */
export function todayIso(): string {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Dhaka', year: 'numeric', month: '2-digit', day: '2-digit' })
    .formatToParts(new Date())
    .reduce<Record<string, string>>((acc, p) => ({ ...acc, [p.type]: p.value }), {});
  return `${parts['year']}-${parts['month']}-${parts['day']}`;
}

/** The hospital's calendar day as a local-midnight Date, for date pickers and comparisons. */
export function hospitalToday(): Date {
  return fromIsoDate(todayIso())!;
}
