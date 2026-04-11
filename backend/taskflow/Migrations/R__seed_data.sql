INSERT INTO users (id, name, email, password) VALUES (
    'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
    'Test User',
    'test@example.com',
    -- bcrypt hash of 'password123' at cost 12
    '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj2NkJ4c5Gyq'
) ON CONFLICT DO NOTHING;

INSERT INTO projects (id, name, description, owner_id) VALUES (
    'b1eebc99-9c0b-4ef8-bb6d-6bb9bd380a22',
    'Demo Project',
    'A sample project for testing',
    'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11'
) ON CONFLICT DO NOTHING;

INSERT INTO tasks (title, status, priority, project_id, assignee_id) VALUES
    ('Design the schema',   'done',        'high',   'b1eebc99-9c0b-4ef8-bb6d-6bb9bd380a22', 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11'),
    ('Build auth endpoints','in_progress', 'high',   'b1eebc99-9c0b-4ef8-bb6d-6bb9bd380a22', 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11'),
    ('Write README',        'todo',        'medium', 'b1eebc99-9c0b-4ef8-bb6d-6bb9bd380a22', NULL);