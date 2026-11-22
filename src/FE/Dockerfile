# ---- Base Node ----
FROM node:20-alpine AS base
WORKDIR /app
COPY package*.json ./

# ---- Dependencies ----
FROM base AS dependencies
RUN npm i

# ---- Build ----
FROM dependencies AS build
COPY . .
RUN npm run build

# ---- Production ----
FROM node:20-alpine AS production
WORKDIR /app
COPY --from=dependencies /app/node_modules ./node_modules
COPY --from=build /app/.next ./.next
COPY --from=build /app/public ./public
COPY --from=build /app/package*.json ./
COPY --from=build /app/next.config.mjs ./next.config.mjs

# Expose the port the app will run on
EXPOSE 3000

# Start the application
CMD ["npm", "start"]
