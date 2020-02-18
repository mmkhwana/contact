CREATE TABLE [dbo].[Contacts]
(
	[Id] INT NOT NULL PRIMARY KEY, 
    [Name] VARCHAR(50) NOT NULL, 
    [Surname] VARCHAR(50) NULL, 
    [CellNumber] VARCHAR(50) NULL, 
    [Email] VARCHAR(50) NULL, 
    [Address] VARCHAR(50) NULL
)
