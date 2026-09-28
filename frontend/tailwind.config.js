/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,jsx}"],
  theme: {
    extend: {
      colors: {
        eger: {
          bg: "#07110e",
          panel: "#0d1c16",
          card: "#12261e",
          line: "#234536",
          green: "#1b5c3c",
          mint: "#3dbe7a",
          amber: "#b7791f",
          gold: "#e3b15a",
        },
      },
      fontFamily: {
        sans: ["Manrope", "Segoe UI", "sans-serif"],
      },
    },
  },
  plugins: [],
};
