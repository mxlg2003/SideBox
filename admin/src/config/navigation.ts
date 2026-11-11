import type { ComponentType } from "react";
import { KeyRound, LayoutDashboard, Rocket, Ticket, Timer } from "lucide-react";

export interface NavItem {
  title: string;
  to: string;
  icon: ComponentType<{ className?: string }>;
}

export interface NavGroup {
  title: string;
  items: NavItem[];
}

export const navGroups: NavGroup[] = [
  {
    title: "总览",
    items: [{ title: "仪表盘", to: "/", icon: LayoutDashboard }],
  },
  {
    title: "客户端",
    items: [{ title: "版本发布", to: "/releases", icon: Rocket }],
  },
  {
    title: "许可证",
    items: [
      { title: "激活码", to: "/licenses", icon: Ticket },
      { title: "支付订单", to: "/orders", icon: KeyRound },
      { title: "试用", to: "/trials", icon: Timer },
    ],
  },
];
