import { SVGProps } from 'react';

type IconProps = SVGProps<SVGSVGElement> & { size?: number };

function Svg({ size = 18, children, ...props }: IconProps & { children: React.ReactNode }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.75}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      {...props}
    >
      {children}
    </svg>
  );
}

export const ArrowRight = (p: IconProps) => <Svg {...p}><path d="M5 12h14M13 6l6 6-6 6" /></Svg>;
export const ArrowLeft = (p: IconProps) => <Svg {...p}><path d="M19 12H5M11 18l-6-6 6-6" /></Svg>;
export const Check = (p: IconProps) => <Svg {...p}><path d="M5 12.5l4.5 4.5L19 7.5" /></Svg>;
export const CheckCircle = (p: IconProps) => <Svg {...p}><circle cx="12" cy="12" r="9" /><path d="M8 12.5l2.5 2.5L16 9.5" /></Svg>;
export const Lock = (p: IconProps) => <Svg {...p}><rect x="4.5" y="10.5" width="15" height="10" rx="2" /><path d="M8 10.5V7.5a4 4 0 018 0v3" /></Svg>;
export const Shield = (p: IconProps) => <Svg {...p}><path d="M12 3l7.5 3v5.5c0 4.5-3.2 8.3-7.5 9.5-4.3-1.2-7.5-5-7.5-9.5V6L12 3z" /><path d="M9 12l2 2 4-4" /></Svg>;
export const Calculator = (p: IconProps) => <Svg {...p}><rect x="5" y="3" width="14" height="18" rx="2" /><path d="M8 7h8M8 11h.01M12 11h.01M16 11h.01M8 15h.01M12 15h.01M16 15v3M8 18h.01M12 18h.01" /></Svg>;
export const FileText = (p: IconProps) => <Svg {...p}><path d="M14 3H7a2 2 0 00-2 2v14a2 2 0 002 2h10a2 2 0 002-2V8l-5-5z" /><path d="M14 3v5h5M9 13h6M9 17h6M9 9h2" /></Svg>;
export const Calendar = (p: IconProps) => <Svg {...p}><rect x="3.5" y="5" width="17" height="15.5" rx="2" /><path d="M3.5 10h17M8 3v4M16 3v4" /></Svg>;
export const Building = (p: IconProps) => <Svg {...p}><path d="M4 21V5a2 2 0 012-2h8a2 2 0 012 2v16M16 9h2a2 2 0 012 2v10M3 21h18M8 7h4M8 11h4M8 15h4" /></Svg>;
export const UserIcon = (p: IconProps) => <Svg {...p}><circle cx="12" cy="8" r="4" /><path d="M4.5 20.5c1.2-3.6 4-5.5 7.5-5.5s6.3 1.9 7.5 5.5" /></Svg>;
export const Briefcase = (p: IconProps) => <Svg {...p}><rect x="3" y="7" width="18" height="13" rx="2" /><path d="M8.5 7V5.5A1.5 1.5 0 0110 4h4a1.5 1.5 0 011.5 1.5V7M3 12.5h18" /></Svg>;
export const Home = (p: IconProps) => <Svg {...p}><path d="M4 10.5L12 4l8 6.5V19a1.5 1.5 0 01-1.5 1.5H5.5A1.5 1.5 0 014 19v-8.5z" /><path d="M9.5 20.5v-6h5v6" /></Svg>;
export const BarChart = (p: IconProps) => <Svg {...p}><path d="M4 20h16M7 16v-5M12 16V7M17 16v-8" /></Svg>;
export const Download = (p: IconProps) => <Svg {...p}><path d="M12 4v11M7 10.5l5 5 5-5M5 20h14" /></Svg>;
export const Info = (p: IconProps) => <Svg {...p}><circle cx="12" cy="12" r="9" /><path d="M12 11v5M12 7.5h.01" /></Svg>;
export const AlertTriangle = (p: IconProps) => <Svg {...p}><path d="M10.3 4.2L2.8 17.5A2 2 0 004.5 20.5h15a2 2 0 001.7-3L13.7 4.2a2 2 0 00-3.4 0z" /><path d="M12 9.5v4M12 17h.01" /></Svg>;
export const Eye = (p: IconProps) => <Svg {...p}><path d="M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12z" /><circle cx="12" cy="12" r="3" /></Svg>;
export const EyeOff = (p: IconProps) => <Svg {...p}><path d="M10.6 5.6A9.7 9.7 0 0112 5.5c6 0 9.5 6.5 9.5 6.5a17 17 0 01-2.7 3.4M6.6 6.6C3.9 8.3 2.5 12 2.5 12S6 18.5 12 18.5c1.9 0 3.6-.6 5-1.5M9.9 9.9a3 3 0 004.2 4.2M3 3l18 18" /></Svg>;
export const X = (p: IconProps) => <Svg {...p}><path d="M6 6l12 12M18 6L6 18" /></Svg>;
export const Menu = (p: IconProps) => <Svg {...p}><path d="M4 7h16M4 12h16M4 17h16" /></Svg>;
export const ChevronDown = (p: IconProps) => <Svg {...p}><path d="M6 9l6 6 6-6" /></Svg>;
export const ChevronRight = (p: IconProps) => <Svg {...p}><path d="M9 6l6 6-6 6" /></Svg>;
export const Trash = (p: IconProps) => <Svg {...p}><path d="M4 7h16M10 11v6M14 11v6M6 7l1 12a2 2 0 002 2h6a2 2 0 002-2l1-12M9 7V5a1 1 0 011-1h4a1 1 0 011 1v2" /></Svg>;
export const Settings = (p: IconProps) => <Svg {...p}><circle cx="12" cy="12" r="3" /><path d="M19.4 15a1.7 1.7 0 00.3 1.8l.1.1a2 2 0 11-2.8 2.8l-.1-.1a1.7 1.7 0 00-1.8-.3 1.7 1.7 0 00-1 1.5V21a2 2 0 11-4 0v-.1a1.7 1.7 0 00-1.1-1.5 1.7 1.7 0 00-1.8.3l-.1.1a2 2 0 11-2.8-2.8l.1-.1a1.7 1.7 0 00.3-1.8 1.7 1.7 0 00-1.5-1H3a2 2 0 110-4h.1a1.7 1.7 0 001.5-1.1 1.7 1.7 0 00-.3-1.8l-.1-.1a2 2 0 112.8-2.8l.1.1a1.7 1.7 0 001.8.3H9a1.7 1.7 0 001-1.5V3a2 2 0 114 0v.1a1.7 1.7 0 001 1.5 1.7 1.7 0 001.8-.3l.1-.1a2 2 0 112.8 2.8l-.1.1a1.7 1.7 0 00-.3 1.8V9a1.7 1.7 0 001.5 1H21a2 2 0 110 4h-.1a1.7 1.7 0 00-1.5 1z" /></Svg>;
export const CreditCard = (p: IconProps) => <Svg {...p}><rect x="3" y="5.5" width="18" height="13" rx="2" /><path d="M3 10h18M7 15h3" /></Svg>;
export const LogOut = (p: IconProps) => <Svg {...p}><path d="M15 4h3a2 2 0 012 2v12a2 2 0 01-2 2h-3M10 16l-4-4 4-4M6 12h10" /></Svg>;
export const MessageCircle = (p: IconProps) => <Svg {...p}><path d="M20.5 11.5a8.4 8.4 0 01-12.2 7.5L3.5 20.5l1.5-4.6a8.5 8.5 0 1115.5-4.4z" /></Svg>;
export const ClipboardCheck = (p: IconProps) => <Svg {...p}><rect x="5" y="4.5" width="14" height="16.5" rx="2" /><path d="M9 4.5V3.5h6v1M9 13l2 2 4-4" /></Svg>;
export const Plus = (p: IconProps) => <Svg {...p}><path d="M12 5v14M5 12h14" /></Svg>;
export const Clock = (p: IconProps) => <Svg {...p}><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></Svg>;
export const ExternalLink = (p: IconProps) => <Svg {...p}><path d="M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 01-1 1H5a1 1 0 01-1-1V7a1 1 0 011-1h5" /></Svg>;
export const Receipt = (p: IconProps) => <Svg {...p}><path d="M6 3h12v18l-3-2-3 2-3-2-3 2V3z" /><path d="M9 8h6M9 12h6M9 16h3" /></Svg>;
export const Layers = (p: IconProps) => <Svg {...p}><path d="M12 3l9 5-9 5-9-5 9-5z" /><path d="M3 13l9 5 9-5" /></Svg>;
export const Scale = (p: IconProps) => <Svg {...p}><path d="M12 4v16M7 20h10M5 8h14M5 8l-2.5 6a3 3 0 005 0L5 8zM19 8l-2.5 6a3 3 0 005 0L19 8z" /></Svg>;
export const Phone = (p: IconProps) => <Svg {...p}><rect x="7" y="2.5" width="10" height="19" rx="2" /><path d="M11 18.5h2" /></Svg>;
export const Grid = (p: IconProps) => <Svg {...p}><rect x="4" y="4" width="7" height="7" rx="1.5" /><rect x="13" y="4" width="7" height="7" rx="1.5" /><rect x="4" y="13" width="7" height="7" rx="1.5" /><rect x="13" y="13" width="7" height="7" rx="1.5" /></Svg>;
export const Sun = (p: IconProps) => <Svg {...p}><circle cx="12" cy="12" r="4" /><path d="M12 2.5v2M12 19.5v2M4.6 4.6L6 6M18 18l1.4 1.4M2.5 12h2M19.5 12h2M4.6 19.4L6 18M18 6l1.4-1.4" /></Svg>;
export const Moon = (p: IconProps) => <Svg {...p}><path d="M20 14.5A8 8 0 019.5 4a8 8 0 1010.5 10.5z" /></Svg>;
export const Monitor = (p: IconProps) => <Svg {...p}><rect x="3" y="4" width="18" height="12.5" rx="2" /><path d="M8.5 20.5h7M12 16.5v4" /></Svg>;
