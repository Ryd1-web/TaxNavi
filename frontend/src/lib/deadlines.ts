import { ComplianceDeadline } from '@/types';

export interface UpcomingDeadline {
  deadline: ComplianceDeadline;
  date: Date;
  daysAway: number;
  period: string;
}

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

function clampDay(year: number, month: number, day: number) {
  const last = new Date(year, month + 1, 0).getDate();
  return new Date(year, month, Math.min(day, last));
}

/**
 * Expands the configured deadlines into the next `count` dated occurrences from `from`.
 * Monthly deadlines fall in the month after the period they cover.
 */
export function upcomingDeadlines(deadlines: ComplianceDeadline[], audiences: string[], from = new Date(), monthsAhead = 12): UpcomingDeadline[] {
  const start = new Date(from.getFullYear(), from.getMonth(), from.getDate());
  const end = new Date(start.getFullYear(), start.getMonth() + monthsAhead, start.getDate());
  const relevant = deadlines.filter((d) => audiences.length === 0 || d.appliesTo.some((a) => audiences.includes(a)));
  const items: UpcomingDeadline[] = [];

  for (const deadline of relevant) {
    if (deadline.frequency === 'Monthly') {
      for (let offset = 0; offset <= monthsAhead; offset++) {
        const date = clampDay(start.getFullYear(), start.getMonth() + offset, deadline.dayOfMonth);
        if (date < start || date > end) continue;
        const covered = new Date(date.getFullYear(), date.getMonth() - 1, 1);
        items.push({ deadline, date, daysAway: diffDays(start, date), period: `${MONTHS[covered.getMonth()]} ${covered.getFullYear()}` });
      }
    } else if (deadline.month) {
      for (const year of [start.getFullYear(), start.getFullYear() + 1]) {
        const date = clampDay(year, deadline.month - 1, deadline.dayOfMonth);
        if (date < start || date > end) continue;
        items.push({ deadline, date, daysAway: diffDays(start, date), period: `Tax year ${year - 1}` });
      }
    }
  }

  return items.sort((a, b) => a.date.getTime() - b.date.getTime());
}

function diffDays(from: Date, to: Date) {
  return Math.round((to.getTime() - from.getTime()) / 86_400_000);
}

export function audiencesFor(userType: 'Individual' | 'SME', options: { employee?: boolean; selfEmployed?: boolean; vatRegistered?: boolean; employer?: boolean } = {}) {
  const audiences = new Set<string>();
  if (userType === 'SME') {
    audiences.add('Company');
    audiences.add('Employer');
    if (options.vatRegistered !== false) audiences.add('VatRegistered');
  } else {
    if (options.employee !== false) audiences.add('Employee');
    if (options.selfEmployed) audiences.add('SelfEmployed');
    if (options.vatRegistered) audiences.add('VatRegistered');
  }
  if (options.employer) audiences.add('Employer');
  return Array.from(audiences);
}
