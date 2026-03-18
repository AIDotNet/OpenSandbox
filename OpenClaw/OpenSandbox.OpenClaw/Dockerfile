FROM node:20 AS web-build
WORKDIR /src/OpenClaw/web
COPY OpenClaw/web/package.json /src/OpenClaw/web/
COPY OpenClaw/web/tsconfig.json /src/OpenClaw/web/
COPY OpenClaw/web/tsconfig.app.json /src/OpenClaw/web/
COPY OpenClaw/web/tsconfig.node.json /src/OpenClaw/web/
COPY OpenClaw/web/vite.config.ts /src/OpenClaw/web/
COPY OpenClaw/web/eslint.config.js /src/OpenClaw/web/
COPY OpenClaw/web/components.json /src/OpenClaw/web/
COPY OpenClaw/web/index.html /src/OpenClaw/web/
COPY OpenClaw/web/public /src/OpenClaw/web/public
COPY OpenClaw/web/src /src/OpenClaw/web/src
RUN npm install
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
WORKDIR /src
COPY . .
COPY --from=web-build /src/OpenClaw/web/dist /src/OpenClaw/web/dist
RUN dotnet publish OpenClaw/OpenSandbox.OpenClaw/OpenSandbox.OpenClaw.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OpenSandbox.OpenClaw.dll"]
