-- Script untuk menambahkan user admin MOLDED dengan role Admin
-- Database: DB_SUPPLY_MOLDED
-- Table: users

-- Hapus user lama jika ada (optional)
DELETE FROM users WHERE username = 'adminMolded321';

-- Insert user admin baru
INSERT INTO users (username, password, created_date, last_login) 
VALUES ('adminMolded321', 'molded321', GETDATE(), NULL);

-- Insert juga user adminMolded (tanpa angka) sebagai backup
INSERT INTO users (username, password, created_date, last_login) 
VALUES ('adminMolded', 'molded321', GETDATE(), NULL);

-- Verifikasi data yang dimasukkan
SELECT * FROM users WHERE username IN ('adminMolded321', 'adminMolded');

-- Catatan: 
-- 1. Role "Admin" ditentukan di AccountController.cs baris 146
-- 2. Setelah update AccountController, username "adminMolded321" akan mendapat role "Admin"
-- 3. Username "adminMolded" juga akan mendapat role "Admin"
-- 4. Password untuk kedua user: "molded321"
