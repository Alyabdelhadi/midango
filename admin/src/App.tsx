import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import { HomeRedirect, RequireRole, RequireStaff } from './app/guards';
import { Shell } from './layout/Shell';
import { AuthProvider, useAuth } from './lib/auth';
import { I18nProvider } from './lib/i18n';
import { AllCustomersPage } from './pages/AllCustomersPage';
import { BookingsPage } from './pages/BookingsPage';
import { CategoriesPage } from './pages/CategoriesPage';
import { CustomerDetailPage } from './pages/CustomerDetailPage';
import { CustomersPage } from './pages/CustomersPage';
import { DeskPage } from './pages/DeskPage';
import { PricesPage } from './pages/PricesPage';
import { ReportsPage } from './pages/ReportsPage';
import { LeadsPage } from './pages/LeadsPage';
import { LegalPage } from './pages/LegalPage';
import { RulesPage } from './pages/RulesPage';
import { SignInPage } from './pages/SignInPage';
import { SchedulePage } from './pages/schedule/SchedulePage';
import { StaffPage } from './pages/StaffPage';
import { TodayPage } from './pages/TodayPage';
import { TournamentPage } from './pages/TournamentPage';
import { TournamentsPage } from './pages/TournamentsPage';
import { VenuePage } from './pages/venue/VenuePage';
import { VenuesPage } from './pages/VenuesPage';
import { Styleguide } from './Styleguide';

function VenueRoute() {
  const { staff } = useAuth();
  return <VenuePage canEdit={staff?.role === 'VenueManager'} />;
}

function PricesRoute() {
  const { staff } = useAuth();
  return <PricesPage canEdit={staff?.role === 'VenueManager'} />;
}

export default function App() {
  return (
    <I18nProvider>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            <Route path="/styleguide" element={<Styleguide />} />
            <Route path="/sign-in" element={<SignInPage />} />
            <Route element={<RequireStaff />}>
              <Route element={<Shell />}>
                <Route index element={<HomeRedirect />} />
                <Route
                  path="categories"
                  element={
                    <RequireRole roles={['PlatformOperator']}>
                      <CategoriesPage />
                    </RequireRole>
                  }
                />
                <Route
                  path="venues"
                  element={
                    <RequireRole roles={['PlatformOperator']}>
                      <VenuesPage />
                    </RequireRole>
                  }
                />
                <Route
                  path="all-customers"
                  element={
                    <RequireRole roles={['PlatformOperator']}>
                      <AllCustomersPage />
                    </RequireRole>
                  }
                />
                <Route
                  path="all-customers/:id"
                  element={
                    <RequireRole roles={['PlatformOperator']}>
                      <CustomerDetailPage />
                    </RequireRole>
                  }
                />
                <Route
                  path="rules"
                  element={
                    <RequireRole roles={['PlatformOperator']}>
                      <RulesPage />
                    </RequireRole>
                  }
                />
                <Route
                  path="leads"
                  element={
                    <RequireRole roles={['PlatformOperator']}>
                      <LeadsPage />
                    </RequireRole>
                  }
                />
                <Route
                  path="legal"
                  element={
                    <RequireRole roles={['PlatformOperator']}>
                      <LegalPage />
                    </RequireRole>
                  }
                />
                {(
                  [
                    ['today', <TodayPage key="today" />],
                    ['desk', <DeskPage key="desk" />],
                    ['schedule', <SchedulePage key="schedule" />],
                    ['bookings', <BookingsPage key="bookings" />],
                    ['customers', <CustomersPage key="customers" />],
                    ['tournaments', <TournamentsPage key="tournaments" />],
                    ['tournaments/:id', <TournamentPage key="tournament" />],
                  ] as const
                ).map(([path, page]) => (
                  <Route
                    key={path}
                    path={path}
                    element={<RequireRole roles={['VenueManager', 'DeskStaff']}>{page}</RequireRole>}
                  />
                ))}
                <Route
                  path="venue"
                  element={
                    <RequireRole roles={['VenueManager', 'DeskStaff']}>
                      <VenueRoute />
                    </RequireRole>
                  }
                />
                <Route
                  path="prices"
                  element={
                    <RequireRole roles={['VenueManager', 'DeskStaff']}>
                      <PricesRoute />
                    </RequireRole>
                  }
                />
                <Route
                  path="reports"
                  element={
                    <RequireRole roles={['VenueManager']}>
                      <ReportsPage />
                    </RequireRole>
                  }
                />
                <Route
                  path="staff"
                  element={
                    <RequireRole roles={['VenueManager']}>
                      <StaffPage />
                    </RequireRole>
                  }
                />
              </Route>
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </I18nProvider>
  );
}
