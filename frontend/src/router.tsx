import { createBrowserRouter } from 'react-router'
import { AnonymousRoute, ProtectedRoute } from './auth/ProtectedRoute'
import { Layout } from './components/Layout'
import { DashboardPage } from './pages/DashboardPage'
import { DocumentDetailsPage } from './pages/DocumentDetailsPage'
import { DocumentsPage } from './pages/DocumentsPage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { RegisterPage } from './pages/RegisterPage'
import { UploadPage } from './pages/UploadPage'

export const router = createBrowserRouter([
  {
    Component: AnonymousRoute,
    children: [
      { path: '/login', Component: LoginPage },
      { path: '/register', Component: RegisterPage },
    ],
  },
  {
    Component: ProtectedRoute,
    children: [
      {
        Component: Layout,
        children: [
          { index: true, Component: DashboardPage },
          { path: '/documents', Component: DocumentsPage },
          { path: '/documents/:id', Component: DocumentDetailsPage },
          { path: '/upload', Component: UploadPage },
        ],
      },
    ],
  },
  { path: '*', Component: NotFoundPage },
])
