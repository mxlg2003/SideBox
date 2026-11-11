/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        ink: "#f5f5f5",
        paper: "#0e0e11",
        line: "#232328",
        mute: "#94949e",
        accent: {
          blue: "#2f6fed",
          cyan: "#38bdf8",
          amber: "#f59e0b",
          violet: "#a855f7",
          emerald: "#10b981",
        },
      },
      fontFamily: {
        sans: [
          "Segoe UI Variable",
          "Segoe UI",
          "Microsoft YaHei UI",
          "PingFang SC",
          "sans-serif",
        ],
        serif: ["Newsreader", "Noto Serif SC", "Songti SC", "Georgia", "serif"],
      },
      maxWidth: {
        page: "1140px",
      },
      animation: {
        marquee: "marquee 45s linear infinite",
        "marquee-reverse": "marquee-reverse 45s linear infinite",
        "pulse-slow": "pulse 4s cubic-bezier(0.4, 0, 0.6, 1) infinite",
      },
      keyframes: {
        marquee: {
          "0%": { transform: "translateX(0%)" },
          "100%": { transform: "translateX(-50%)" },
        },
        "marquee-reverse": {
          "0%": { transform: "translateX(-50%)" },
          "100%": { transform: "translateX(0%)" },
        },
      },
    },
  },
  plugins: [],
};
