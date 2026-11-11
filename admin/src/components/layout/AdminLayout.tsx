import { useState } from "react";
import { LayoutDashboard, PanelLeftClose, PanelLeftOpen } from "lucide-react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/modules/auth/AuthContext";
import { navGroups } from "@/config/navigation";

export function AdminLayout() {
  const { username, logout } = useAuth();
  const navigate = useNavigate();
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem("SideBox_admin_sidebar_collapsed") === "true";
    } catch {
      return false;
    }
  });

  const toggleSidebar = () => {
    setCollapsed((prev) => {
      const next = !prev;
      try {
        localStorage.setItem("SideBox_admin_sidebar_collapsed", String(next));
      } catch {
        // ignore
      }
      return next;
    });
  };

  return (
    <div
      className={`min-h-screen transition-all duration-300 md:grid ${
        collapsed
          ? "md:grid-cols-[var(--sidebar-collapsed-width)_1fr]"
          : "md:grid-cols-[var(--sidebar-width)_1fr]"
      }`}
    >
      <aside
        className={`border-r bg-sidebar text-sidebar-foreground transition-all duration-300 flex flex-col ${
          collapsed ? "px-2 py-4" : "px-3 py-4"
        }`}
      >
        {collapsed ? (
          <div className="flex flex-col items-center gap-2 rounded-lg border border-white/10 bg-white/5 p-2">
            <div className="rounded-md bg-sidebar-primary p-1.5 text-sidebar-primary-foreground" title="SideBox Admin">
              <LayoutDashboard className="size-4" />
            </div>
            <button
              type="button"
              onClick={toggleSidebar}
              title="展开侧边栏"
              className="rounded p-1 text-sidebar-foreground/60 hover:bg-white/10 hover:text-white transition-colors"
            >
              <PanelLeftOpen className="size-4" />
            </button>
          </div>
        ) : (
          <div className="flex items-center justify-between rounded-lg border border-white/10 bg-white/5 p-3">
            <div className="flex items-center gap-2.5 min-w-0">
              <div className="rounded-md bg-sidebar-primary p-1.5 text-sidebar-primary-foreground shrink-0">
                <LayoutDashboard className="size-4" />
              </div>
              <div className="min-w-0 truncate">
                <p className="text-[10px] uppercase tracking-[0.2em] text-sidebar-foreground/70 leading-none">SideBox</p>
                <h1 className="text-sm font-semibold truncate leading-tight mt-0.5">Admin</h1>
              </div>
            </div>
            <button
              type="button"
              onClick={toggleSidebar}
              title="收起侧边栏"
              className="rounded p-1 text-sidebar-foreground/60 hover:bg-white/10 hover:text-white transition-colors shrink-0"
            >
              <PanelLeftClose className="size-4" />
            </button>
          </div>
        )}

        <nav className="mt-5 space-y-4 overflow-y-auto flex-1 pr-0.5">
          {navGroups.map((group, groupIdx) => (
            <div key={group.title}>
              {collapsed ? (
                groupIdx > 0 && <div className="my-2 h-px bg-white/10" />
              ) : (
                <p className="mb-1 px-2.5 text-[11px] uppercase tracking-[0.2em] text-sidebar-foreground/50">{group.title}</p>
              )}
              <div className="space-y-1">
                {group.items.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    end={item.to === "/"}
                    title={collapsed ? item.title : undefined}
                    className={({ isActive }) =>
                      [
                        collapsed
                          ? "flex items-center justify-center rounded-md p-2 text-sm transition-colors"
                          : "flex items-center gap-2.5 rounded-md px-2.5 py-1.5 text-sm transition-colors",
                        isActive ? "bg-white/10 text-white font-medium shadow-xs" : "text-sidebar-foreground/80 hover:bg-white/6 hover:text-white",
                      ].join(" ")
                    }
                  >
                    <item.icon className="size-4 shrink-0" />
                    {!collapsed && <span className="truncate">{item.title}</span>}
                  </NavLink>
                ))}
              </div>
            </div>
          ))}
        </nav>
      </aside>

      <div className="flex min-h-screen flex-col min-w-0">
        <header className="border-b bg-background/70 px-5 py-3 backdrop-blur">
          <div className="flex items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <button
                type="button"
                onClick={toggleSidebar}
                title={collapsed ? "展开侧边栏" : "收起侧边栏"}
                className="hidden md:inline-flex rounded-md border p-1.5 text-muted-foreground hover:bg-muted hover:text-foreground transition-colors"
              >
                {collapsed ? <PanelLeftOpen className="size-4" /> : <PanelLeftClose className="size-4" />}
              </button>
              <div>
                <p className="text-xs text-muted-foreground">当前登录</p>
                <p className="text-sm font-medium">{username ?? "--"}</p>
              </div>
            </div>
            <Button
              size="sm"
              variant="outline"
              onClick={() => {
                logout();
                navigate("/login", { replace: true });
              }}
            >
              退出登录
            </Button>
          </div>
        </header>
        <main className="flex-1 p-5 min-w-0">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
