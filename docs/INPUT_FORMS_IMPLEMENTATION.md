# New Web App Features Implementation Summary

## 1. Sidebar Navigation
- Added new top-level menu **"Input Data"** in the sidebar.
- Added two submenus:
  - **Green Hose Input**: Resolves to `/Home/GreenHoseInput`
  - **After Washing Input**: Resolves to `/AfterWashing/AfterWashingInput`

## 2. Green Hose Input Page (`/Home/GreenHoseInput`)
- **Theme**: Green color scheme consistent with Green Hose branding.
- **Tabs**: 
  - **INPUT IN**: Recording material entry into storage.
  - **INPUT OUT**: Recording material exit for supply.
- **Features**:
  - **QR Scanner**: Integrated HTML5-Qrcode library for camera scanning.
  - **FIFO Logic**: In "INPUT OUT" mode, displays item recommendations based on the oldest production date (First-In, First-Out).
  - **Data Submission**: Saves to `storage_log` (IN) and `supply_log` (OUT).

## 3. After Washing Input Page (`/AfterWashing/AfterWashingInput`)
- **Theme**: Blue color scheme consistent with After Washing branding.
- **Tabs**:
  - **INPUT IN**: Recording materials entering After Washing racks.
  - **INPUT OUT**: Recording materials leaving for Finishing.
- **Features**:
  - **Transaction Type**: Radio buttons for "IN" vs "SISA" in Input IN mode.
  - **QR Scanner**: Integrated camera scanning.
  - **Planning Button**: Added placeholder button for future functionality.
  - **Data Submission**: Saves to `storage_log_aw` table.

## 4. Backend Changes
- **HomeController.cs**:
  - Added `GreenHoseInput` action.
  - Added `GetFIFORecommendations` API (queries `storage_log` for oldest items).
  - Added `SubmitGreenHoseInput` API.
  - *Note*: Temporarily commented out `ProductionDate` assignment for `SupplyLog` as the property doesn't exist in the model yet.
- **AfterWashingController.cs**:
  - Added `AfterWashingInput` action.
  - Added `SubmitAfterWashingInput` API.
- **InputModels.cs**:
  - Created new model classes `GreenHoseInputModel` and `AfterWashingInputModel` for type-safe form handling.

## Next Steps / Recommendations
- **Database Update**: The `supply_log` table and model need a `ProductionDate` column if you wish to track production dates for items leaving storage.
- **Planning Feature**: The Planning button in After Washing is currently a placeholder and needs implementation specs.
