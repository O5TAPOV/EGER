import RegisterPage from "./RegisterPage";

export default function ProfessorDashboard() {
  return <RegisterPage subjectsPath="/subjects/mine" linkBase="/professor/students" showTwoFactor />;
}
