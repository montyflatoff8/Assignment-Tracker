-- Schema for the Assignment Tracker database.
-- Docker runs this automatically the first time the db container starts with an empty volume.
-- It is safe to run again by hand (psql -f init-schema.sql), e.g. against Azure PostgreSQL.

CREATE TABLE IF NOT EXISTS assignments (
    id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title       VARCHAR(100) NOT NULL,
    course      VARCHAR(60)  NOT NULL,
    due_date    DATE         NOT NULL,
    status      VARCHAR(20)  NOT NULL DEFAULT 'todo'
                CHECK (status IN ('todo', 'in-progress', 'done')),
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Supports filtering by status (WHERE status = 'todo'); used in the Part 4 query baseline.
CREATE INDEX IF NOT EXISTS idx_assignments_status ON assignments(status);

-- Starter rows (same as the old in-memory seed data), only if the table is empty.
INSERT INTO assignments (title, course, due_date, status)
SELECT * FROM (VALUES
    ('Midterm project',   'Web Programming 1', DATE '2026-10-25', 'in-progress'),
    ('Chapter 6 reading', 'Databases',         DATE '2026-10-22', 'todo')
) AS seed(title, course, due_date, status)
WHERE NOT EXISTS (SELECT 1 FROM assignments);
