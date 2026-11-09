# My Tournament

## Stage 8: Role based access

The app supports `SuperAdmin`, `Host`, `Player`, and `Guest` roles. New registrations receive the `Player` role by default. After sign-in, users go to the home page for their role. Each role home page checks the active `UserSession`; logged-out users are sent to sign-in, and users with a different role are sent to the no-permission page.

Role home routes: `/admin`, `/host`, `/player`, and `/guest`.

## Stage 9: Inner pages

Role workspaces now include responsive dashboard summaries and role-specific navigation. The admin workspace includes a sample user directory; each role has a tournament directory, and hosts and players have a team roster page. Inner pages use illustrative sample data only; search, invitations, sign-ups, and management controls are design mockups and are not connected to the database.
