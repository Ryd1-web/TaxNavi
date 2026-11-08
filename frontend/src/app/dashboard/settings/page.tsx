'use client';

import { FormEvent, useState } from 'react';
import { Button } from '@/components/ui/Button';
import { ChoiceCard, TextInput } from '@/components/ui/Form';
import { Building, UserIcon } from '@/components/ui/Icons';
import { Alert, Card, CardHeader, PageHeader } from '@/components/ui/Primitives';
import { ThemeSelect } from '@/components/ui/ThemeToggle';
import { useAuth } from '@/context/AuthContext';
import { clearProgress } from '@/lib/calculator';
import { apiService, ApiError } from '@/services/apiService';
import { AccountType } from '@/types';

export default function SettingsPage() {
  const { user, setUser } = useAuth();
  const [form, setForm] = useState({ firstName: user?.firstName ?? '', lastName: user?.lastName ?? '', userType: (user?.userType ?? 'Individual') as AccountType });
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ tone: 'success' | 'error'; text: string } | null>(null);
  const [cleared, setCleared] = useState(false);

  if (!user) return null;

  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (!form.firstName.trim() || !form.lastName.trim()) {
      setMessage({ tone: 'error', text: 'Enter your first and last name.' });
      return;
    }
    setSaving(true);
    setMessage(null);
    try {
      const updated = await apiService.updateProfile({ ...form, firstName: form.firstName.trim(), lastName: form.lastName.trim() });
      setUser({ ...user, ...updated, userType: updated.userType === 'SME' ? 'SME' : 'Individual' });
      setMessage({ tone: 'success', text: 'Your profile has been updated.' });
    } catch (err) {
      setMessage({ tone: 'error', text: (err as ApiError).message });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader eyebrow="Account" title="Settings" />

      <Card>
        <CardHeader title="Profile" description="Your account type decides which dashboard and tools you see." />
        <form onSubmit={save} className="space-y-5 p-5 sm:p-6" noValidate>
          {message && <Alert tone={message.tone}>{message.text}</Alert>}
          <div className="grid gap-5 sm:grid-cols-2">
            <TextInput label="First name" value={form.firstName} onChange={(e) => setForm((f) => ({ ...f, firstName: e.target.value }))} autoComplete="given-name" maxLength={100} />
            <TextInput label="Last name" value={form.lastName} onChange={(e) => setForm((f) => ({ ...f, lastName: e.target.value }))} autoComplete="family-name" maxLength={100} />
          </div>
          <TextInput label="Email" value={user.email} disabled hint="Contact support to change your email address." />
          <fieldset>
            <legend className="label">Account type</legend>
            <div className="grid gap-3 sm:grid-cols-2">
              <ChoiceCard name="type" checked={form.userType === 'Individual'} onChange={() => setForm((f) => ({ ...f, userType: 'Individual' }))} icon={<UserIcon />} title="Personal" description="Employees & freelancers" />
              <ChoiceCard name="type" checked={form.userType === 'SME'} onChange={() => setForm((f) => ({ ...f, userType: 'SME' }))} icon={<Building />} title="Business" description="SMEs & companies" />
            </div>
          </fieldset>
          <Button type="submit" loading={saving} loadingText="Saving…">
            Save changes
          </Button>
        </form>
      </Card>

      <Card>
        <CardHeader title="Appearance" description="Choose a theme, or follow your device setting. Saved on this device." />
        <div className="p-5 sm:p-6">
          <ThemeSelect />
        </div>
      </Card>

      <Card>
        <CardHeader title="Privacy on this device" />
        <div className="space-y-3 p-5 sm:p-6">
          <p className="text-sm leading-6 text-ink-muted">
            Unfinished calculator answers are kept in this browser so you can resume. Clear them if you use a shared device.
          </p>
          {cleared && <Alert tone="success">Calculator answers cleared from this device.</Alert>}
          <Button
            variant="secondary"
            onClick={() => {
              clearProgress();
              setCleared(true);
            }}
          >
            Clear saved calculator answers
          </Button>
        </div>
      </Card>
    </div>
  );
}
