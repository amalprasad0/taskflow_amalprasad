INSERT INTO users (id, name, email, password) VALUES (
    'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
    'Test User',
    'test@example.com',
    '$2a$11$EZ3FzHfpPZU1XPRaIUj3qertIKd3kof5l6fNEXynihEuSdMcITF1m'
) ON CONFLICT DO NOTHING;

INSERT INTO projects (id, name, description, owner_id) VALUES (
    'b1eebc99-9c0b-4ef8-bb6d-6bb9bd380a22',
    'Demo Project',
    'A sample project for testing',
    'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11'
) ON CONFLICT DO NOTHING;

INSERT INTO tasks (title, status, priority, project_id, assignee_id, created_by) VALUES
    ('Design the schema',   'done',        'high',   'b1eebc99-9c0b-4ef8-bb6d-6bb9bd380a22', 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11', 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11'),
    ('Build auth endpoints','in_progress', 'high',   'b1eebc99-9c0b-4ef8-bb6d-6bb9bd380a22', 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11', 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11'),
    ('Write README',        'todo',        'medium', 'b1eebc99-9c0b-4ef8-bb6d-6bb9bd380a22', NULL, 'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11');