-- Check for duplicate user roles in AspNetUserRoles table
SELECT 
    anur.UserId,
    anu.Email,
    anur.RoleId,
    anr.Name AS RoleName,
    COUNT(*) AS Count
FROM AspNetUserRoles anur
INNER JOIN AspNetUsers anu ON anur.UserId = anu.Id
INNER JOIN AspNetRoles anr ON anur.RoleId = anr.Id
GROUP BY anur.UserId, anu.Email, anur.RoleId, anr.Name
HAVING COUNT(*) > 1;

-- Check all roles for users that might have the issue
SELECT 
    anu.Email,
    anr.Name AS RoleName,
    anur.UserId,
    anur.RoleId
FROM AspNetUserRoles anur
INNER JOIN AspNetUsers anu ON anur.UserId = anu.Id
INNER JOIN AspNetRoles anr ON anur.RoleId = anr.Id
WHERE anu.Email LIKE '%@%'  -- Adjust this to filter specific users
ORDER BY anu.Email, anr.Name;

-- Find users with multiple employee roles (excluding Customer)
SELECT 
    anu.Email,
    COUNT(DISTINCT anr.Name) AS RoleCount,
    STRING_AGG(anr.Name, ', ') AS Roles
FROM AspNetUserRoles anur
INNER JOIN AspNetUsers anu ON anur.UserId = anu.Id
INNER JOIN AspNetRoles anr ON anur.RoleId = anr.Id
WHERE anr.Name != 'Customer'
GROUP BY anu.Email
HAVING COUNT(DISTINCT anr.Name) > 1;
