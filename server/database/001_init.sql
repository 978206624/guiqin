-- 归秦开发库的首版结构，目标版本为 MySQL 8.0。
CREATE DATABASE IF NOT EXISTS `guiqin`
  CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_cs;
USE `guiqin`;

CREATE TABLE IF NOT EXISTS `account` (
  `id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
  `channel` VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `external_id` VARCHAR(191) COLLATE utf8mb4_bin NOT NULL,
  `password_hash` VARCHAR(255) NULL,
  `status` TINYINT UNSIGNED NOT NULL DEFAULT 0 COMMENT '0 正常，1 封禁',
  `created_at` TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  UNIQUE KEY `uq_account_identity` (`channel`, `external_id`)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `role` (
  `id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
  `account_id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `zone_id` VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `name` VARCHAR(24) COLLATE utf8mb4_bin NOT NULL,
  `state_json` JSON NOT NULL,
  `ver` BIGINT UNSIGNED NOT NULL DEFAULT 0,
  `created_at` TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  UNIQUE KEY `uq_role_zone_name` (`zone_id`, `name`),
  KEY `ix_role_account_zone` (`account_id`, `zone_id`),
  CONSTRAINT `fk_role_account` FOREIGN KEY (`account_id`) REFERENCES `account` (`id`),
  CONSTRAINT `ck_role_state_object` CHECK (JSON_TYPE(`state_json`) = 'OBJECT')
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `chat_msg` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `zone_id` VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `channel` VARCHAR(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `scope_id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT '',
  `sender_role_id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
  `recipient_role_id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
  `payload_json` JSON NOT NULL,
  `created_at` TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  KEY `ix_chat_channel_history` (`zone_id`, `channel`, `scope_id`, `id`),
  KEY `ix_chat_sender_history` (`sender_role_id`, `id`),
  KEY `ix_chat_recipient_history` (`recipient_role_id`, `id`),
  CONSTRAINT `ck_chat_payload_object` CHECK (JSON_TYPE(`payload_json`) = 'OBJECT')
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `friend` (
  `role_id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `friend_role_id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `created_at` TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`role_id`, `friend_role_id`),
  KEY `ix_friend_peer` (`friend_role_id`),
  CONSTRAINT `fk_friend_owner` FOREIGN KEY (`role_id`) REFERENCES `role` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_friend_peer` FOREIGN KEY (`friend_role_id`) REFERENCES `role` (`id`) ON DELETE CASCADE,
  CONSTRAINT `ck_friend_distinct` CHECK (`role_id` <> `friend_role_id`)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `battle_log` (
  `battle_id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
  `role_id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `stage_id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `seed` INT NOT NULL,
  `rules_version` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `initial_state_json` JSON NOT NULL,
  `commands_json` JSON NOT NULL,
  `result_json` JSON NOT NULL,
  `created_at` TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  KEY `ix_battle_role_time` (`role_id`, `created_at`),
  CONSTRAINT `ck_battle_commands_array` CHECK (JSON_TYPE(`commands_json`) = 'ARRAY')
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `item_flow` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `role_id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `asset_type` VARCHAR(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `asset_id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `before_value` BIGINT NOT NULL,
  `after_value` BIGINT NOT NULL,
  `reason` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `reference_id` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
  `created_at` TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  KEY `ix_flow_role_history` (`role_id`, `id`),
  KEY `ix_flow_reason_reference` (`reason`, `reference_id`)
) ENGINE=InnoDB;
