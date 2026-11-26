-- Placement Dashboard Database Schema (SQLite / ADO.NET)
CREATE TABLE IF NOT EXISTS Placements (
    PlacementId INTEGER PRIMARY KEY AUTOINCREMENT,
    StudentName TEXT NOT NULL,
    Department TEXT NOT NULL,
    Company TEXT NOT NULL,
    PackageLPA REAL NOT NULL CHECK (PackageLPA >= 0),
    PlacementStatus TEXT NOT NULL CHECK (PlacementStatus IN ('Placed', 'Shortlisted', 'Pending')),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
