'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { FormEvent, Suspense, useEffect, useState } from 'react';
import AuthLayout, { safeNext } from '@/components/layout/AuthLayout';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/ui/Form';
import { Alert } from '@/components/ui/Primitives';
import { useAuth } from '@/context/AuthContext';
import { ApiError } from '@/services/apiService';

function LoginForm() {
  const router = useRouter();
  const params = useSearchParams();
  const next = safeNext(params.get('next'));
  const { login, isAuthenticated, isLoading } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [errors, setErrors] = useState<{ email?: string; password?: string; form?: string }>({});
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!isLoading && isAuthenticated) router.replace(next);
  }, [isLoading, isAuthenticated, next, router]);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const nextErrors: typeof errors = {};
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) nextErrors.email = 'Enter a valid email address.';
    if (!password) nextErrors.password = 'Enter your password.';
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length) return;

    setSubmitting(true);
    try {
      await login(email.trim(), password);
      router.replace(next);
    } catch (err) {
      setErrors({ form: (err as ApiError).message });
      setSubmitting(false);
    }
  };

  return (
    <AuthLayout
      title="Welcome back"
      subtitle={
        <>
          New to TaxNavi?{' '}
          <Link className="link" href={`/auth/register?next=${encodeURIComponent(next)}`}>
            Create a free account
          </Link>
        </>
      }
    >
      <form onSubmit={onSubmit} noValidate className="space-y-5">
        {errors.form && <Alert tone="error">{errors.form}</Alert>}
        <TextInput label="Email" type="email" autoComplete="email" inputMode="email" value={email} onChange={(e) => setEmail(e.target.value)} error={errors.email} />
        <div>
          <TextInput
            label="Password"
            type={showPassword ? 'text' : 'password'}
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={errors.password}
          />
          <button type="button" className="mt-2 text-[13px] font-medium text-ink-subtle hover:text-ink" onClick={() => setShowPassword((v) => !v)} aria-pressed={showPassword}>
            {showPassword ? 'Hide password' : 'Show password'}
          </button>
        </div>
        <Button type="submit" block size="lg" loading={submitting} loadingText="Signing in…">
          Sign in
        </Button>
      </form>
      <p className="mt-6 text-center text-sm text-ink-subtle">
        Just want an estimate?{' '}
        <Link className="link" href="/calculate">
          Calculate without an account
        </Link>
      </p>
    </AuthLayout>
  );
}

export default function LoginPage() {
  return (
    <Suspense>
      <LoginForm />
    </Suspense>
  );
}
