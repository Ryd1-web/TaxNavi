'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { FormEvent, Suspense, useEffect, useState } from 'react';
import AuthLayout, { safeNext } from '@/components/layout/AuthLayout';
import { Button } from '@/components/ui/Button';
import { ChoiceCard, PasswordInput, TextInput } from '@/components/ui/Form';
import { Building, Check, UserIcon } from '@/components/ui/Icons';
import { Alert } from '@/components/ui/Primitives';
import { useAuth } from '@/context/AuthContext';
import { ApiError } from '@/services/apiService';
import { AccountType } from '@/types';

const passwordRules = [
  { test: (p: string) => p.length >= 8, label: 'At least 8 characters' },
  { test: (p: string) => /[A-Z]/.test(p), label: 'An uppercase letter' },
  { test: (p: string) => /[a-z]/.test(p), label: 'A lowercase letter' },
  { test: (p: string) => /[0-9]/.test(p), label: 'A number' },
];

type Errors = Partial<Record<'firstName' | 'lastName' | 'email' | 'password' | 'form', string>>;

function RegisterForm() {
  const router = useRouter();
  const params = useSearchParams();
  const next = safeNext(params.get('next'));
  const { register, isAuthenticated, isLoading } = useAuth();
  const [form, setForm] = useState({
    firstName: '',
    lastName: '',
    email: '',
    password: '',
    userType: (params.get('type') === 'SME' || next.includes('type=sme') ? 'SME' : 'Individual') as AccountType,
  });
  const [errors, setErrors] = useState<Errors>({});
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!isLoading && isAuthenticated && !submitting) router.replace(next);
  }, [isLoading, isAuthenticated, next, router, submitting]);

  const update = (key: keyof typeof form) => (event: React.ChangeEvent<HTMLInputElement>) => setForm((f) => ({ ...f, [key]: event.target.value }));

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const nextErrors: Errors = {};
    if (!form.firstName.trim()) nextErrors.firstName = 'Enter your first name.';
    if (!form.lastName.trim()) nextErrors.lastName = 'Enter your last name.';
    if (!/^\S+@\S+\.\S+$/.test(form.email.trim())) nextErrors.email = 'Enter a valid email address.';
    if (!passwordRules.every((rule) => rule.test(form.password))) nextErrors.password = 'Your password needs to meet all the requirements below.';
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length) return;

    setSubmitting(true);
    try {
      await register({ ...form, email: form.email.trim(), firstName: form.firstName.trim(), lastName: form.lastName.trim() });
      router.replace(next);
    } catch (err) {
      setErrors({ form: (err as ApiError).message });
      setSubmitting(false);
    }
  };

  return (
    <AuthLayout
      title="Create your account"
      subtitle={
        <>
          Already have one?{' '}
          <Link className="link" href={`/auth/login?next=${encodeURIComponent(next)}`}>
            Sign in
          </Link>
        </>
      }
    >
      <form onSubmit={onSubmit} noValidate className="space-y-5">
        {errors.form && <Alert tone="error">{errors.form}</Alert>}
        <fieldset>
          <legend className="label">Account type</legend>
          <div className="grid gap-3 sm:grid-cols-2">
            <ChoiceCard name="userType" checked={form.userType === 'Individual'} onChange={() => setForm((f) => ({ ...f, userType: 'Individual' }))} icon={<UserIcon />} title="Personal" description="Employees & freelancers" />
            <ChoiceCard name="userType" checked={form.userType === 'SME'} onChange={() => setForm((f) => ({ ...f, userType: 'SME' }))} icon={<Building />} title="Business" description="SMEs & companies" />
          </div>
        </fieldset>
        <div className="grid gap-5 sm:grid-cols-2">
          <TextInput label="First name" autoComplete="given-name" value={form.firstName} onChange={update('firstName')} error={errors.firstName} maxLength={100} />
          <TextInput label="Last name" autoComplete="family-name" value={form.lastName} onChange={update('lastName')} error={errors.lastName} maxLength={100} />
        </div>
        <TextInput label="Email" type="email" autoComplete="email" inputMode="email" value={form.email} onChange={update('email')} error={errors.email} />
        <div>
          <PasswordInput label="Password" autoComplete="new-password" value={form.password} onChange={update('password')} error={errors.password} maxLength={128} />
          <ul className="mt-2.5 grid grid-cols-2 gap-1.5" aria-label="Password requirements">
            {passwordRules.map((rule) => {
              const ok = rule.test(form.password);
              return (
                <li key={rule.label} className={`flex items-center gap-1.5 text-[13px] ${ok ? 'text-brand-700' : 'text-ink-subtle'}`}>
                  <Check size={14} strokeWidth={ok ? 3 : 2} className={ok ? '' : 'opacity-40'} />
                  {rule.label}
                  <span className="sr-only">{ok ? '(met)' : '(not met)'}</span>
                </li>
              );
            })}
          </ul>
        </div>
        <Button type="submit" block size="lg" loading={submitting} loadingText="Creating account…">
          Create account
        </Button>
        <p className="text-center text-[13px] leading-5 text-ink-subtle">We only ask for what we need to set up your account.</p>
      </form>
    </AuthLayout>
  );
}

export default function RegisterPage() {
  return (
    <Suspense>
      <RegisterForm />
    </Suspense>
  );
}
