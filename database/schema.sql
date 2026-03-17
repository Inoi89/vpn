create extension if not exists "pgcrypto";

create table if not exists nodes (
    id uuid primary key,
    agent_identifier varchar(128) not null unique,
    name varchar(200) not null,
    cluster varchar(128) not null,
    agent_base_address varchar(512) not null,
    certificate_thumbprint varchar(128),
    description varchar(1024),
    status varchar(32) not null,
    agent_version varchar(64),
    last_seen_at_utc timestamptz,
    last_error varchar(2048),
    is_enabled boolean not null default true,
    created_at_utc timestamptz not null,
    updated_at_utc timestamptz not null
);

create table if not exists users (
    id uuid primary key,
    external_id varchar(128) not null unique,
    display_name varchar(200) not null,
    email varchar(256),
    is_enabled boolean not null default true,
    created_at_utc timestamptz not null,
    updated_at_utc timestamptz not null
);

create unique index if not exists ux_users_email on users(email);

create table if not exists peer_configs (
    id uuid primary key,
    node_id uuid not null references nodes(id) on delete cascade,
    user_id uuid not null references users(id) on delete restrict,
    display_name varchar(200) not null,
    public_key varchar(128) not null,
    protocol_flavor varchar(64) not null,
    allowed_ips varchar(1024) not null,
    metadata_json jsonb,
    revision integer not null,
    last_synced_at_utc timestamptz not null,
    is_enabled boolean not null default true,
    created_at_utc timestamptz not null,
    updated_at_utc timestamptz not null
);

create unique index if not exists ux_peer_configs_node_public_key on peer_configs(node_id, public_key);
create index if not exists ix_peer_configs_user_id on peer_configs(user_id);

create table if not exists sessions (
    id uuid primary key,
    node_id uuid not null references nodes(id) on delete cascade,
    user_id uuid not null references users(id) on delete restrict,
    peer_config_id uuid references peer_configs(id) on delete set null,
    peer_public_key varchar(128) not null,
    endpoint varchar(256),
    state varchar(32) not null,
    connected_at_utc timestamptz,
    last_handshake_at_utc timestamptz,
    last_observed_at_utc timestamptz not null,
    last_rx_bytes bigint not null,
    last_tx_bytes bigint not null,
    created_at_utc timestamptz not null,
    updated_at_utc timestamptz not null
);

create unique index if not exists ux_sessions_node_public_key on sessions(node_id, peer_public_key);
create index if not exists ix_sessions_state_last_observed on sessions(state, last_observed_at_utc desc);

create table if not exists traffic_stats (
    id uuid primary key,
    node_id uuid not null references nodes(id) on delete cascade,
    user_id uuid not null references users(id) on delete restrict,
    session_id uuid references sessions(id) on delete set null,
    peer_config_id uuid references peer_configs(id) on delete set null,
    rx_bytes bigint not null,
    tx_bytes bigint not null,
    captured_at_utc timestamptz not null,
    created_at_utc timestamptz not null,
    updated_at_utc timestamptz not null
);

create index if not exists ix_traffic_stats_node_user_captured
    on traffic_stats(node_id, user_id, captured_at_utc desc);

create index if not exists ix_traffic_stats_captured
    on traffic_stats(captured_at_utc desc);
