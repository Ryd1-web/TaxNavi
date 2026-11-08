import PublicLayout from '@/components/layout/PublicLayout';
import { ButtonLink } from '@/components/ui/Button';

export default function NotFound() {
  return (
    <PublicLayout>
      <div className="container-page flex flex-col items-center py-24 text-center">
        <p className="eyebrow">404</p>
        <h1 className="mt-2 text-3xl font-semibold">We couldn&apos;t find that page</h1>
        <p className="mt-3 max-w-md text-ink-muted">The link may be out of date. You can start a new calculation or head back home.</p>
        <div className="mt-8 flex flex-wrap justify-center gap-3">
          <ButtonLink href="/calculate">Calculate my tax</ButtonLink>
          <ButtonLink href="/" variant="secondary">Go home</ButtonLink>
        </div>
      </div>
    </PublicLayout>
  );
}
