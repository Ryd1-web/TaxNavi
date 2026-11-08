import { redirect } from 'next/navigation';

export default function SmeTaxRedirect() {
  redirect('/calculate?type=sme');
}
