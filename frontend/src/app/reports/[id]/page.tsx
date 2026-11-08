import { redirect } from 'next/navigation';

export default function ReportRedirect({ params }: { params: { id: string } }) {
  redirect(`/dashboard/reports/${encodeURIComponent(params.id)}`);
}
