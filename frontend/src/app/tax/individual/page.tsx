import { redirect } from 'next/navigation';

export default function IndividualTaxRedirect() {
  redirect('/calculate?type=individual');
}
