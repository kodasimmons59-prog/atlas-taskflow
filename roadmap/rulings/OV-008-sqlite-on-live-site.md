# OV-008 Use SQLite on the live site

Date: 2026-11-09 · From: Koda

Asked: "Should sign-in work on the live Azure site this sprint?" The brief asks for sign-in to be verified in the
deployed app, and Azure for Students had refused to create Azure SQL in every region (DR-0009).

> Yes, use SQLite on the live site

(Chosen on a decision card. The option said: the live site gets a small database file inside the App Service, so
sign-in and saved data work online; Koda's PC keeps LocalDB; nothing new to create in Azure.)

Applied in: PLAN-003, PLAN-004, DR-0010.
