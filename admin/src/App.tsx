import { Navigate, Route, Routes } from "react-router-dom";
import { AdminLayout } from "@/components/layout/AdminLayout";
import { ProtectedRoute } from "@/components/layout/ProtectedRoute";
import { LoginPage } from "@/pages/LoginPage";
import { DashboardPage } from "@/pages/DashboardPage";
import { LicensesPage } from "@/pages/LicensesPage";
import { OrdersPage } from "@/pages/OrdersPage";
import { TrialsPage } from "@/pages/TrialsPage";
import { ReleasesPage } from "@/pages/ReleasesPage";

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <AdminLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<DashboardPage />} />
        <Route path="releases" element={<ReleasesPage />} />
        <Route path="licenses" element={<LicensesPage />} />
        <Route path="orders" element={<OrdersPage />} />
        <Route path="trials" element={<TrialsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
